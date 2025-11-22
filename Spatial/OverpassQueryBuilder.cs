using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndoorCO2MapAppV2.Spatial
{
    internal static class OverpassQueryBuilder
    {

        internal static string CreateTransportOverpassQuery(double latitude, double longitude, double radius, bool startLocation)
        {
            string rString = radius.ToString(CultureInfo.InvariantCulture);
            string latString = latitude.ToString(CultureInfo.InvariantCulture);
            string lonString = longitude.ToString(CultureInfo.InvariantCulture);

            //TODO: don't query lines if it isnt start but destination request
            // Construct the Overpass query with the specified radius and location, only for tram stops
            if (startLocation)
            {
                return "[out:json];" +
                    "(" +
                    $"nwr(around:{rString},{latString},{lonString})[railway=tram_stop];" +
                    $"relation(around:{rString},{latString},{lonString})[route=tram];" +
                    $"nwr(around:{rString},{latString},{lonString})[highway=bus_stop];" +
                    $"relation(around:{rString},{latString},{lonString})[route=bus];" +
                    $"nwr(around:{rString},{latString},{lonString})[railway=subway_station];" +
                    $"relation(around:{rString},{latString},{lonString})[route=subway];" +
                    $"nwr(around:{rString},{latString},{lonString})[railway=station];" +
                    $"nwr(around:{rString},{latString},{lonString})[railway=halt];" +
                    $"nwr(around:{rString},{latString},{lonString})[railway=stop];" +
                    $"relation(around:{rString},{latString},{lonString})[route=train];" +
                    $"relation(around:{rString},{latString},{lonString})[route=light_rail];" +
                    $"relation(around:{rString},{latString},{lonString})[route=monorail];" +

                    ");" +
                    "out center tags qt;";
            }
            else //doesnt include transit lines as we just need target location
            {
                return "[out:json];" +
                  "(" +
                  $"nwr(around:{rString},{latString},{lonString})[railway=tram_stop];" +
                  $"nwr(around:{rString},{latString},{lonString})[highway=bus_stop];" +
                  $"nwr(around:{rString},{latString},{lonString})[railway=subway_station];" +
                  $"nwr(around:{rString},{latString},{lonString})[railway=station];" +
                  $"nwr(around:{rString},{latString},{lonString})[railway=stop];" +
                  $"nwr(around:{rString},{latString},{lonString})[railway=halt];" +

                  ");" +
                  "out center tags qt;";
            }
        }

        internal static string CreateBuildingOverpassQuery(double latitude, double longitude, double radius)
        {
            string rString = radius.ToString(CultureInfo.InvariantCulture);
            string latString = latitude.ToString(CultureInfo.InvariantCulture);
            string lonString = longitude.ToString(CultureInfo.InvariantCulture);
            // Construct the Overpass query with the specified radius and location
            //TODO: add remaining categories of amenities and maybe other
            return "[out:json];" +
                "(" +
                $"nwr(around:{rString},{latString},{lonString})[office=employment_agency];" +
                $"nwr(around:{rString},{latString},{lonString})[office=lawyer];" +
                $"nwr(around:{rString},{latString},{lonString})[office=educational_institution];" +
                $"nwr(around:{rString},{latString},{lonString})[office=government];" +
                $"nwr(around:{rString},{latString},{lonString})[office=political_party];" +
                $"nwr(around:{rString},{latString},{lonString})[office=coworking];" +
                $"nwr(around:{rString},{latString},{lonString})[government=register_office];" +
                $"nwr(around:{rString},{latString},{lonString})[shop];" +
                $"nwr(around:{rString},{latString},{lonString})[craft];" +
                $"nwr(around:{rString},{latString},{lonString})[aeroway=aerodrome];" +
                $"nwr(around:{rString},{latString},{lonString})[aeroway=terminal];" +
                $"nwr(around:{rString},{latString},{lonString})[railway=station];" +
                $"nwr(around:{rString},{latString},{lonString})[public_transport=station];" +
                $"nwr(around:{rString},{latString},{lonString})[leisure=fitness_centre];" +
                $"nwr(around:{rString},{latString},{lonString})[leisure=bowling_alley];" +
                $"nwr(around:{rString},{latString},{lonString})[leisure=sports_centre];" +
                $"nwr(around:{rString},{latString},{lonString})[leisure=sports_hall];" +
                $"nwr(around:{rString},{latString},{lonString})[sport=swimming];" +
                $"nwr(around:{rString},{latString},{lonString})[leisure=swimming_pool];" +
                $"nwr(around:{rString},{latString},{lonString})[leisure=sauna];" +
                $"nwr(around:{rString},{latString},{lonString})[leisure=hackerspace];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=townhall];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=car_rental];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=convention_centre];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=conference_centre];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=congress_centre];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=events_centre];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=bar];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=place_of_worship];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=pub];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=restaurant];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=cafe];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=fast_food];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=food_court];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=ice_cream];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=college];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=dancing_school];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=driving_school];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=kindergarten];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=language_school];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=library];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=cinema];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=theatre];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=concert_hall];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=music_venue];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=arts_centre];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=brothel];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=love_hotel];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=nightclub];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=planetarium];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=stripclub];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=social_centre];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=community_centre];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=playground][indoor=yes];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=research_institute];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=music_school];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=school];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=townhall];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=courthouse];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=post_office];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=university];" +
                $"nwr(around:{rString},{latString},{lonString})[building=university];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=hospital];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=clinic];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=dentist];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=doctors];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=pharmacy];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=veterinary];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=social_facility];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=bank];" +
                $"nwr(around:{rString},{latString},{lonString})[healthcare];" +
                $"nwr(around:{rString},{latString},{lonString})[tourism=museum];" +
                $"nwr(around:{rString},{latString},{lonString})[tourism=attraction];" +
                $"nwr(around:{rString},{latString},{lonString})[tourism=zoo];" +
                $"nwr(around:{rString},{latString},{lonString})[tourism=gallery];" +
                $"nwr(around:{rString},{latString},{lonString})[tourism=hotel];" +
                $"nwr(around:{rString},{latString},{lonString})[tourism][building];" +
                ");" +
                "out center qt;";
        }


    }
}
