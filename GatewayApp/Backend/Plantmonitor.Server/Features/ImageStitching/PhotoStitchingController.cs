using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.ImageStitching;
using Plantmonitor.Shared.Features.ImageStreaming;

namespace Plantmonitor.Server.Features.DeviceProgramming;

[ApiController]
[Route("api/[controller]")]
public class PhotoStitchingController(IDataContext context, IVirtualImageWorker virtualImageWorker, IImageCropper cropper)
{
    public record struct AddPlantModel(IEnumerable<PlantModel> Plants, long TourId);
    public record struct PhotoTourPlantInfo(long Id, string Name, string Comment, string? QrCode, long PhotoTourFk, IEnumerable<ExtractionMetaData> ExtractionMetaData);
    public record struct ExtractionMetaData(long TripWithExtraction, int MotorPosition, DateTime ExtractionTime);
    public record struct PlantExtractionTemplateModel(long Id, long PhotoTripFk, long PhotoTourPlantFk, NpgsqlPolygon PhotoBoundingBox, NpgsqlPoint IrBoundingBoxOffset, int MotorPosition, DateTime ApplicablePhotoTripFrom);
    public record struct PlantModel(string Name, string Comment, string QrCode);
    public record struct IrOffsetFineAdjustement(long ExtractionTemplateId, NpgsqlPoint NewIrOffset, bool OverwriteOffset);
    public record struct PlantImageSection(int StepCount, long PhotoTripId, NpgsqlPolygon Polygon, NpgsqlPoint IrPolygonOffset, long PlantId);
    public record struct ImageCropPreview(byte[] IrImage, byte[] VisImage, NpgsqlPoint CurrentOffset, NpgsqlPoint IrImageSize);

    [HttpGet("plantsfortour")]
    public IEnumerable<PhotoTourPlantInfo> PlantsForTour(long tourId)
    {
        return context.PhotoTourPlants
            .Include(ptp => ptp.PlantExtractionTemplates)
            .ThenInclude(pet => pet.PhotoTripFkNavigation)
            .Where(ptp => ptp.PhotoTourFk == tourId)
            .Select(ptp => new PhotoTourPlantInfo(ptp.Id, ptp.Name, ptp.Comment, ptp.QrCode, ptp.PhotoTourFk, ptp.PlantExtractionTemplates
                .Select(pet => new ExtractionMetaData(pet.PhotoTripFk, pet.MotorPosition, pet.PhotoTripFkNavigation.Timestamp))));
    }

    [HttpGet("extractionsoftrip")]
    public IEnumerable<PlantExtractionTemplateModel> ExtractionsOfTrip(long tripId)
    {
        var trip = context.PhotoTourTrips
            .First(ptt => ptt.Id == tripId);
        var extractionTemplates = context.PlantExtractionTemplates
            .Include(pet => pet.PhotoTripFkNavigation)
            .Where(pet => pet.PhotoTripFkNavigation.Timestamp <= trip.Timestamp && pet.PhotoTripFkNavigation.PhotoTourFk == trip.PhotoTourFk)
            .GroupBy(pet => pet.PhotoTourPlantFk)
            .Select(g => g.OrderByDescending(pet => pet.PhotoTripFkNavigation.Timestamp).FirstOrDefault())
            .ToList();
        return extractionTemplates
            .Where(t => t != null)
            .Select(t => new PlantExtractionTemplateModel(t!.Id, t.PhotoTripFk, t.PhotoTourPlantFk, t.PhotoBoundingBox, t.IrBoundingBoxOffset,
            t.MotorPosition, t.PhotoTripFkNavigation.Timestamp));
    }

    [HttpGet("tripsoftour")]
    public IEnumerable<PhotoTourTrip> TripsOfTour(long tourId)
    {
        return context.PhotoTourTrips
            .OrderBy(ptt => ptt.Timestamp)
            .Where(ptt => ptt.PhotoTourFk == tourId);
    }

    [HttpPost("removeplants")]
    public void RemovePlantsFromTour(long[] plantIds)
    {
        var referencedPlants = context.PlantExtractionTemplates
            .Include(pet => pet.PhotoTourPlantFkNavigation)
            .Where(pet => plantIds.Contains(pet.PhotoTourPlantFk))
            .ToList();
        if (referencedPlants.Count > 0)
            throw new Exception($"The following plants have extraction plans and cannot be removed: {referencedPlants.Select(rp => rp.PhotoTourPlantFkNavigation.Name).Concat(", ")}");
        context.PhotoTourPlants.RemoveRange(context.PhotoTourPlants.Where(ptp => plantIds.Contains(ptp.Id)));
        context.SaveChanges();
    }

    [HttpGet("croppedimagefor")]
    public ImageCropPreview CroppedImageFor(long extractionTemplateId, long photoTripId)
    {
        var template = context.PlantExtractionTemplates.First(pet => pet.Id == extractionTemplateId);
        var photoTrip = context.PhotoTourTrips.First(ptt => ptt.Id == photoTripId);
        var irFile = CameraStreamFormatter.FindInFolder(photoTrip.IrDataFolder, template.MotorPosition);
        var visFile = CameraStreamFormatter.FindInFolder(photoTrip.VisDataFolder, template.MotorPosition);
        if (visFile.FileName == null) throw new Exception($"Vis image for position {template.MotorPosition} could not be found in {photoTrip.VisDataFolder}");
        if (irFile.FileName == null) throw new Exception($"Ir image for position {template.MotorPosition} could not be found in {photoTrip.IrDataFolder}");
        var (visImage, irImage) = cropper.CropImages(visFile.FileName, irFile.FileName, [.. template.PhotoBoundingBox],
            template.IrBoundingBoxOffset, VirtualImageWorker.VirtualPlantImageCropHeight);
        if (visImage == null || irImage == null)
        {
            visImage?.Dispose();
            irImage?.Dispose();
            throw new Exception("Cropping was not successfull");
        }
        cropper.ApplyIrColorMap(irImage);
        var irImageSize = new NpgsqlPoint(irImage.Cols, irImage.Rows);
        return new ImageCropPreview()
        {
            IrImage = cropper.MatToByteArray(irImage),
            VisImage = cropper.MatToByteArray(visImage),
            CurrentOffset = template.IrBoundingBoxOffset,
            IrImageSize = irImageSize
        };
    }

    [HttpPost("updateiroffset")]
    public void UpdateIrOFfset(IrOffsetFineAdjustement adjustment)
    {
        var template = context.PlantExtractionTemplates
            .First(pet => pet.Id == adjustment.ExtractionTemplateId);
        var offset = template.IrBoundingBoxOffset;
        var adjustedOffset = new NpgsqlPoint(offset.X + adjustment.NewIrOffset.X, offset.Y + adjustment.NewIrOffset.Y);
        template.IrBoundingBoxOffset = adjustment.OverwriteOffset ? adjustment.NewIrOffset : adjustedOffset;
        context.SaveChanges();
    }

    [HttpPost("associateplantimagesection")]
    public void AssociatePlantImageSection(PlantImageSection section)
    {
        var maxWidth = section.Polygon.Max(p => p.X) - section.Polygon.Min(p => p.X);
        var maxHeight = section.Polygon.Max(p => p.Y) - section.Polygon.Min(p => p.Y);
        var trip = context.PhotoTourTrips
            .Include(ptt => ptt.PlantExtractionTemplates)
            .First(ptt => ptt.Id == section.PhotoTripId);
        if (trip.PlantExtractionTemplates.Any(pet => pet.PhotoTourPlantFk == section.PlantId))
            throw new Exception("Each plant may only have one Polygon per Phototrip");
        context.PlantExtractionTemplates.Add(
        new PlantExtractionTemplate()
        {
            PhotoBoundingBox = section.Polygon,
            MotorPosition = section.StepCount,
            PhotoTripFk = section.PhotoTripId,
            PhotoTourPlantFk = section.PlantId,
            IrBoundingBoxOffset = section.IrPolygonOffset,
            BoundingBoxHeight = (float)maxHeight,
            BoundingBoxWidth = (float)maxWidth
        });
        context.SaveChanges();
    }

    [HttpPost("removeplantimagesection")]
    public void RemovePlantImageSections(long[] sectionIds)
    {
        context.PlantExtractionTemplates.RemoveRange(context.PlantExtractionTemplates.Where(pet => sectionIds.Contains(pet.Id)));
        context.SaveChanges();
    }

    [HttpPost("recalculatephototour")]
    public void RecalculatePhotoTour(long photoTourFk)
    {
        virtualImageWorker.RecalculateTour(photoTourFk);
    }

    [HttpPost("addplantstotour")]
    public void AddPlantsToTour(AddPlantModel plants)
    {
        var newNames = plants.Plants.Select(p => p.Name).ToHashSet();
        var newQrCodes = plants.Plants.Select(p => p.QrCode).ToHashSet();
        var existingNames = context.PhotoTourPlants.Where(ptp => ptp.PhotoTourFk == plants.TourId && newNames.Contains(ptp.Name));
        var existingQrCodes = context.PhotoTourPlants.Where(ptp => ptp.PhotoTourFk == plants.TourId && ptp.QrCode != null && ptp.QrCode.Length > 0 && newQrCodes.Contains(ptp.QrCode));
        if (existingNames.Any() || existingQrCodes.Any())
        {
            var message = $"The following names exist already: {existingNames.Select(en => en.Name).Concat(", ")}\n The following QR-Codes exist already: {existingQrCodes.Select(qr => qr.QrCode).Concat(", ")}";
            throw new Exception(message);
        }
        context.PhotoTourPlants.AddRange(plants.Plants.Select(p => new PhotoTourPlant()
        {
            Comment = p.Comment,
            Name = p.Name,
            PhotoTourFk = plants.TourId,
            QrCode = p.QrCode,
        }));
        context.SaveChanges();
    }
}
