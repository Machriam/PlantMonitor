﻿using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Plantmonitor.Server.Features.AppConfiguration;
using Plantmonitor.Server.Features.DeviceConfiguration;
using Plantmonitor.Shared.Features.ImageStreaming;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Plantmonitor.Server.Features.DeviceControl
{
    public class PictureStreamingHub(IEnvironmentConfiguration configuration, ILogger<PictureStreamingHub> logger, IDeviceApiFactory factory,
        IDeviceConnectionEventBus deviceConnections) : Hub
    {
        private static readonly ConcurrentDictionary<string, (string Ip, StreamingMetaData Device)> s_ipByConnectionId = new();

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (s_ipByConnectionId.TryGetValue(Context.ConnectionId, out var data))
            {
                if (data.Device.GetCameraType() == CameraType.IR) await factory.IrImageTakingClient(data.Ip).KillcameraAsync();
                else await factory.VisImageTakingClient(data.Ip).KillcameraAsync();
            };
            await base.OnDisconnectedAsync(exception);
        }

        public async Task<ChannelReader<StoredDataStream>> CustomStreamAsZip(StreamingMetaData data, string ip, CancellationToken token)
        {
            var deviceId = deviceConnections.GetDeviceHealthInformation().First(h => h.Ip == ip).Health?.DeviceId;
            s_ipByConnectionId.TryAdd(Context.ConnectionId, (ip, data));
            var picturePath = data.StoreData && deviceId != null ? configuration.PicturePath(deviceId) : "";
            var channel = Channel.CreateBounded<StoredDataStream>(new BoundedChannelOptions(1)
            {
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = true,
            });
            var connection = new HubConnectionBuilder()
                .WithUrl($"https://{ip}/hub/video")
                .AddMessagePackProtocol()
                .Build();
            await connection.StartAsync(token);
            StoreDataOnDeviceStream(data, picturePath, channel, connection, ip, token).RunInBackground(ex => ex.LogError());
            return channel.Reader;
        }

        public async Task<ChannelReader<CameraStreamData>> StreamPictures(StreamingMetaData data, string ip, CancellationToken token)
        {
            var deviceId = deviceConnections.GetDeviceHealthInformation().First(h => h.Ip == ip).Health?.DeviceId;
            s_ipByConnectionId.TryAdd(Context.ConnectionId, (ip, data));
            var picturePath = data.StoreData && deviceId != null ? configuration.PicturePath(deviceId) : "";
            var channel = Channel.CreateBounded<CameraStreamData>(new BoundedChannelOptions(1)
            {
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = true,
            });
            var connection = new HubConnectionBuilder()
                .WithUrl($"https://{ip}/hub/video")
                .AddMessagePackProtocol()
                .Build();
            await connection.StartAsync(token);
            StreamData(data, picturePath, channel, connection, token).RunInBackground(ex => ex.LogError());
            return channel.Reader;
        }

        public ChannelReader<CameraStreamData> StreamPictureSeries(string deviceId, string sequenceId)
        {
            var channel = Channel.CreateBounded<CameraStreamData>(new BoundedChannelOptions(1)
            {
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true,
            });
            var directory = Path.Combine(configuration.PicturePath(deviceId), sequenceId);
            if (!Path.Exists(directory)) throw new Exception($"Path {directory} could not be found");
            var files = Directory.EnumerateFiles(directory).OrderBy(f => f).ToList();
            StreamFiles(files, channel).RunInBackground(ex => ex.LogError());
            return channel.Reader;
        }

        private async Task StreamFiles(IList<string> fileList, Channel<CameraStreamData> channel)
        {
            foreach (var file in fileList)
            {
                var success = CameraStreamFormatter.FromFile(file, out var streamData);
                if (!success) continue;
                await channel.Writer.WriteAsync(streamData.ConvertToStreamObject());
                await Task.Delay(10);
            }
            await Task.Delay(5000);
            Context.Abort();
        }

        private async Task StoreDataOnDeviceStream(StreamingMetaData data, string picturePath, Channel<StoredDataStream> channel, HubConnection connection, string ip, CancellationToken token)
        {
            var cameraInfo = data.GetCameraType().Attribute<CameraTypeInfo>();
            var stream = await connection.StreamAsChannelAsync<StoredDataStream>(cameraInfo.CustomStorageMethod, data, token);
            var lastMessage = new StoredDataStream();
            while (await stream.WaitToReadAsync(token))
            {
                await foreach (var image in stream.ReadAllAsync(token))
                {
                    lastMessage = image;
                    var result = await channel.Writer.WriteAsync(image, token).Try();
                    if (!result.IsEmpty()) logger.LogWarning("Could not write Picturestream {error}", result);
                }
            }
            var downloadResult = await factory.StaticFilesClient(ip).DownloadToStaticFiles(lastMessage.ZipFileName, async s =>
            {
                lastMessage.DownloadStatus = s;
                var result = await channel.Writer.WriteAsync(lastMessage, token).Try();
                if (!result.IsEmpty()) logger.LogWarning("Could not update download status {error}", result);
            }).Try();
            if (!downloadResult.Error.IsEmpty()) logger.LogError("Could not download zip from device {error}", downloadResult.Error);
            lastMessage.DownloadStatus = 1;
            await channel.Writer.WriteAsync(lastMessage, token);
        }

        private async Task StreamData(StreamingMetaData data, string picturePath, Channel<CameraStreamData> channel, HubConnection connection, CancellationToken token)
        {
            var cameraInfo = data.GetCameraType().Attribute<CameraTypeInfo>();
            var sequenceId = DateTime.Now.ToString(CameraStreamFormatter.PictureDateFormat);
            var stream = await connection.StreamAsChannelAsync<byte[]>(cameraInfo.SignalRMethod, data, token);
            var path = Path.Combine(picturePath, sequenceId);
            if (!picturePath.IsEmpty()) Directory.CreateDirectory(path);
            while (await stream.WaitToReadAsync(token))
            {
                await foreach (var image in stream.ReadAllAsync(token))
                {
                    var cameraStream = CameraStreamFormatter.FromBytes(image);
                    if (!picturePath.IsEmpty() && cameraStream.PictureData != null)
                    {
                        cameraStream.WriteToFile(path, cameraInfo);
                    }
                    var result = await channel.Writer.WriteAsync(cameraStream.ConvertToStreamObject(), token).Try();
                    if (!result.IsEmpty()) logger.LogWarning("Could not write Picturestream {error}", result);
                }
            }
        }
    }
}
