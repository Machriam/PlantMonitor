export enum SelectedDashboardTab {
    plantSummary = 1,
    virtualPhotoViewer = 2,
}
export const DasboardTabDescriptions = new Map<SelectedDashboardTab, string>([
    [SelectedDashboardTab.plantSummary, "Plant Summary"],
    [SelectedDashboardTab.virtualPhotoViewer, "Virtual Photos"]
]);