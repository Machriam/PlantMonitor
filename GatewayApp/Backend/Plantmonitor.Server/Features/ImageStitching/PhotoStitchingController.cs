using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using Plantmonitor.DataModel.DataModel;

namespace Plantmonitor.Server.Features.DeviceProgramming;

[ApiController]
[Route("api/[controller]")]
public class PhotoStitchingController(IDataContext context)
{
    public record struct AddPlantModel(IEnumerable<PlantModel> Plants, long TourId);
    public record struct PhotoTourPlantInfo(long Id, string Name, string Comment, string? QrCode, long PhotoTourFk, IEnumerable<PlantExtractionTemplateModel> ExtractionTemplate);
    public record struct PlantExtractionTemplateModel(long Id, long PhotoTripFk, long PhotoTourPlantFk, NpgsqlPolygon PhotoBoundingBox, NpgsqlPoint IrBoundingBoxOffset, int MotorPosition, DateTime ApplicablePhotoTripFrom);
    public record struct PlantModel(string Name, string Comment, string QrCode);
    public record struct PlantImageSection(int StepCount, long PhotoTripId, NpgsqlPolygon Polygon, NpgsqlPoint IrPolygonOffset, long PlantId);

    [HttpGet("plantsfortour")]
    public IEnumerable<PhotoTourPlantInfo> PlantsForTour(long tourId)
    {
        return context.PhotoTourPlants
            .Include(ptp => ptp.PlantExtractionTemplates)
            .ThenInclude(pet => pet.PhotoTripFkNavigation)
            .Where(ptp => ptp.PhotoTourFk == tourId)
            .Select(ptp => new PhotoTourPlantInfo(ptp.Id, ptp.Name, ptp.Comment, ptp.QrCode, ptp.PhotoTourFk, ptp.PlantExtractionTemplates
                .Select(pet => new PlantExtractionTemplateModel(pet.Id, pet.PhotoTripFk, pet.PhotoTourPlantFk,
                               pet.PhotoBoundingBox, pet.IrBoundingBoxOffset, pet.MotorPosition, pet.PhotoTripFkNavigation.Timestamp))));
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

    [HttpPost("associateplantimagesection")]
    public void AssociatePlantImageSection(PlantImageSection section)
    {
        context.PlantExtractionTemplates.Add(
        new PlantExtractionTemplate()
        {
            PhotoBoundingBox = section.Polygon,
            MotorPosition = section.StepCount,
            PhotoTripFk = section.PhotoTripId,
            PhotoTourPlantFk = section.PlantId,
            IrBoundingBoxOffset = section.IrPolygonOffset
        });
        context.SaveChanges();
    }

    [HttpPost("removeplantimagesection")]
    public void RemovePlantImageSections(long[] sectionIds)
    {
        context.PlantExtractionTemplates.RemoveRange(context.PlantExtractionTemplates.Where(pet => sectionIds.Contains(pet.Id)));
        context.SaveChanges();
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
