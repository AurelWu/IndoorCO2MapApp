using System;
using System.Collections.Generic;
using System.Text;

namespace IndoorCO2MapAppV2.PersistentData
{
    public class UserSettings
    {
        public bool SortBuildingsAlphabetical { get; set; } = false;
        public bool DisplaySortingModeToggle { get; set; } = true;
        public bool DisplayBuildingFilterInputField { get; set; } = true;

        public Color DefaultButtonColor { get; set; } = new(50, 50, 100);
        public Color NotPickedToggleButtonColor { get; set; } = new(150, 150, 150);
        public Color PressedButtonColor { get; set; } = new(200, 50, 100);
    }



}
