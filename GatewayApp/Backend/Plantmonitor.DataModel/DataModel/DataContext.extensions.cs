using Microsoft.EntityFrameworkCore;

namespace Plantmonitor.DataModel.DataModel;

public partial class DataContext
{
    private partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DeviceMovement>().Property(p => p.MovementPlan).HasColumnType("jsonb").HasColumnName("movement_plan_json");
        modelBuilder.Entity<DeviceMovement>().Ignore(p => p.MovementPlanJson);
    }
}