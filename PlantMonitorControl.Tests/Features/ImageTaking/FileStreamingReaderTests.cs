using FluentAssertions;
using NSubstitute;
using PlantMonitorControl.Features.ImageTaking;
using System;
using System.Threading.Tasks;
using Xunit;

namespace PlantMonitorControl.Tests.Features.ImageTaking
{
    public class FileStreamingReaderTests
    {
        private FileStreamingReader CreateFileStreamingReader()
        {
            return new FileStreamingReader();
        }

        [Fact]
        public async Task ReadNextFile_Skipping_ShouldWork()
        {
            var creationTime = DateTime.UtcNow;
            var sut = CreateFileStreamingReader();
            var testBytes = new byte[] { 1, 2, 3 };
            Directory.Delete("./testFiles", true);
            var testDir = Directory.CreateDirectory("./testFiles");
            for (var i = 0; i < 20; i++) File.WriteAllBytes(Path.Combine(testDir.FullName, $"{i.ToString(FileStreamingReader.CounterFormat)}.jpg"), testBytes);
            var result = await sut.ReadNextFileWithSkipping(testDir.FullName, 0, 10, default!, CancellationToken.None);
            result.NewCounter.Should().Be(11);
            result.CreationDate.Should().BeAfter(creationTime);
            result.FileData.Should().BeEquivalentTo(testBytes);
            Directory.EnumerateFiles(testDir.FullName).Count().Should().Be(9);
            for (var i = result.NewCounter; i < 19; i++)
            {
                result = await sut.ReadNextFileWithSkipping(testDir.FullName, result.NewCounter, 10, default!, CancellationToken.None);
                result.NewCounter.Should().Be(i + 1);
                result.FileData.Should().BeEquivalentTo(testBytes);
                Directory.EnumerateFiles(testDir.FullName).Count().Should().Be(20 - i - 1);
            }
            result = await sut.ReadNextFileWithSkipping(testDir.FullName, result.NewCounter, 10, default!, CancellationToken.None);
            result.NewCounter.Should().Be(19);
            result.CreationDate.Should().BeBefore(creationTime);
            result.FileData.Should().BeNull();
            result = await sut.ReadNextFileWithSkipping(testDir.FullName, result.NewCounter, 10, default!, CancellationToken.None);
            result.NewCounter.Should().Be(19);
            result.CreationDate.Should().BeBefore(creationTime);
            result.FileData.Should().BeNull();
            Directory.EnumerateFiles(testDir.FullName).Count().Should().Be(1);
        }
    }
}
