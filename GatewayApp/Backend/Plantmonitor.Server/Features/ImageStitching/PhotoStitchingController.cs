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
    public record struct PlantModel(string Name, string Comment, string QrCode);
    public record struct PlantImageSection(int StepCount, long[] PhotoTours, NpgsqlPolygon Polygon, NpgsqlPoint IrPolygonOffset, long PlantId);

    [HttpGet("plantsfortour")]
    public IEnumerable<PhotoTourPlant> PlantsForTour(long tourId)
    {
        return context.PhotoTourPlants.Where(ptp => ptp.PhotoTourFk == tourId);
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
        context.PlantExtractionTemplates.AddRange(section.PhotoTours.Select(pt => new PlantExtractionTemplate()
        {
            PhotoBoundingBox = section.Polygon,
            PhotoTripFk = pt,
            PhotoTourPlantFk = section.PlantId,
            IrBoundingBoxOffset = section.IrPolygonOffset
        }));
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
