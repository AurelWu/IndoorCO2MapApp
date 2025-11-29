
using IndoorCO2MapAppV2.CO2Monitors;

namespace IndoorCO2MapAppV2.Pages
{
	public partial class BuildingRecordingDebugPage : AppPage
	{
		public BuildingRecordingDebugPage()
		{
			InitializeComponent();			
			List<CO2Reading> mockData = [];
			mockData.Add(new CO2Reading(500, 0, DateTime.Now));
            mockData.Add(new CO2Reading(600, 1, DateTime.Now));
            mockData.Add(new CO2Reading(700, 2, DateTime.Now));
            mockData.Add(new CO2Reading(650, 3, DateTime.Now));
            mockData.Add(new CO2Reading(750, 4, DateTime.Now));
            mockData.Add(new CO2Reading(550, 5, DateTime.Now));
            mockData.Add(new CO2Reading(625, 6, DateTime.Now));
			lineChartView.SetData(mockData,0,mockData.Count-1);
			TrimSilder.Maximum = mockData.Count - 1;
		}

        private void OnGenerateRandomDataClicked(object sender, EventArgs e)
        {
			GenerateRandomData();
        }

        public void GenerateRandomData()
		{
            List<CO2Reading> mockData = [];
            Int64 amount = Random.Shared.NextInt64(5, 31);
			for (int i = 0; i < amount; i++)
			{
				mockData.Add(new CO2Reading((int)Random.Shared.NextInt64(450, 2500), i, DateTime.Now));
			}
			lineChartView.SetData(mockData,0,mockData.Count-1);
            TrimSilder.Maximum = mockData.Count - 1;
			TrimSilder.Minimum = 0;
        }
	}
}