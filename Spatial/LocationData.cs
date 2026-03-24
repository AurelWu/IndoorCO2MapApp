using IndoorCO2MapAppV2.PersistentData;
using IndoorCO2MapAppV2.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndoorCO2MapAppV2.Spatial
{
    public record LocationData
    {
        public string Type { get; }
        public long ID { get; }
        public string Name { get; }
        public double Latitude { get; }
        public double Longitude { get; }

        public double Distance { get; private set; }

        public string FavouriteKey => $"{Type}_{ID}";

        public LocationData(string type, long id, string name, double latitude, double longitude, double userLatitude, double userLongitude)
        {
            this.Type = type;
            this.ID = id;
            this.Name = name;
            this.Latitude = latitude;
            this.Longitude = longitude;
            CalculateDistanceToGivenLocation(userLatitude, userLongitude);
        }
        private void CalculateDistanceToGivenLocation(double userLatitude, double userLongitude)
        {
            Distance = Haversine.GetDistanceInMeters(userLatitude, userLongitude, Latitude, Longitude);
        }

        public override string ToString()
        {
            if (Name.Length == 0)
            {
                if (Type == "relation")
                {
                    return "nameless relation " + ID;
                }
                if (Type == "node")
                {
                    return "nameless node " + ID;
                }
                if (Type == "way")
                {
                    return "nameless way " + ID;
                }
                else
                {
                    return "nameless entry " + ID;
                }
            }
            else if (UserSettings.Instance.FavouriteLocationKeys.Contains(FavouriteKey))
            {
                return $"{Name} | {(int)Distance}m ★";
            }
            else
            {
                return $"{Name} | {(int)Distance}m";
            }


        }
    }
}
