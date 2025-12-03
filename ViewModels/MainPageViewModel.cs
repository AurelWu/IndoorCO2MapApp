using IndoorCO2MapAppV2.ViewModels;

public class MainPageViewModel
{
    //Idea is to compose the page ViewModels out of smaller Building Blocks to avoid duplication, maybe we should give the subcomponents other names to differentiate better
    public SensorViewModel Sensor { get; }
    public BuildingSearchViewModel BuildingSearch { get; }

    public MainPageViewModel()
    {
        Sensor = new SensorViewModel();
        BuildingSearch = new BuildingSearchViewModel();
    }
}