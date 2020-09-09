// <copyright>
// Copyright 2020 Stewmystre
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Net;

using Newtonsoft.Json;
using RestSharp;

using Rock;
using Rock.Address;
using Rock.Attribute;

namespace com.stewmystre.Addy.Address
{
    /// <summary>
    /// An address lookup and geocoding service using <a href="https://addy.co.nz/">Addy</a>
    /// </summary>
    [Description("An address verification and geocoding service from Addy")]
    [Export(typeof(VerificationComponent))]
    [ExportMetadata("ComponentName", "Addy")]
    [TextField("API Key", "Your Addy API Key", true, "", "", 2)]
    [TextField("API Secret", "Your Addy API Secret", true, "", "", 2)]
    public class Addy : VerificationComponent
    {
        /// <summary>
        /// Standardizes and Geocodes an address using the Addy service
        /// </summary>
        /// <param name="location">The location</param>
        /// <param name="resultMsg">The result</param>
        /// <returns>
        /// True/False value of whether the verification was successful or not
        /// </returns>
        public override VerificationResult Verify(Rock.Model.Location location, out string resultMsg)
        {
            resultMsg = string.Empty;
            VerificationResult result = VerificationResult.None;

            string apiKey = GetAttributeValue("APIKey");
            string apiSecret = GetAttributeValue("APISecret");

            //Create input streetAddress string to send to Addy
            var addressParts = new[] { location.Street1, location.Street2, location.City, location.State, location.PostalCode };
            string streetAddress = string.Join(" ", addressParts.Where(s => !string.IsNullOrEmpty(s)));

            //Restsharp API request
            var client = new RestClient("https://api.addy.co.nz/");
            var request = BuildRequest(streetAddress, apiKey, apiSecret);
            var response = client.Execute(request);

            if (response.StatusCode == HttpStatusCode.OK)
            //Deserialize response into object
            {
                var settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    MissingMemberHandling = MissingMemberHandling.Ignore
                };
                var addyResponse = JsonConvert.DeserializeObject<AddressVerificationResult>(response.Content, settings);
                var addyAddress = addyResponse.address;
                var addyAddressAlternatives = addyResponse.alternatives;
                if (addyAddress.IsNotNull())
                {
                    location.StandardizeAttemptedResult = addyAddress.linzid.ToString();

                    if (addyAddressAlternatives.IsNull() || addyAddressAlternatives.Count() == 0 )
                    {
                        bool updateResult = UpdateLocation(location, addyAddress);
                        var generalMsg = string.Format("Verified with Addy to match LINZ: {0}. Input address: {1}. ", addyAddress.linzid.ToString(), streetAddress);
                        var standardisedMsg = "Coordinates NOT updated.";
                        var geocodedMsg = "Coordinates updated.";

                        if (updateResult)
                        {
                            //result = VerificationResult.Geocoded;
                            resultMsg = generalMsg + geocodedMsg;
                        }
                        else
                        {
                            //result = VerificationResult.Standardized;
                            resultMsg = generalMsg + standardisedMsg;
                        }
                        result |= VerificationResult.Standardized;
                    }
                    else
                    {
                        resultMsg = string.Format("Not verified: {0}", addyResponse.reason);
                        if (addyAddressAlternatives.Count() > 0)
                        {
                            var tooManyMsg = "Too many to display...";
                            foreach (AddressReference alternate in addyAddressAlternatives)
                            {                            
                                if (resultMsg.Length + alternate.a.Length >= 195)
                                {
                                    if (resultMsg.Length + tooManyMsg.Length <= 200)
                                    {
                                        resultMsg += tooManyMsg;
                                    }
                                    else
                                    {
                                        resultMsg += "...";
                                    }
                                    
                                    break;
                                } else
                                {
                                    resultMsg += alternate.a + "; ";
                                }                                    
                            }
                        }
                    }
                }
                else
                {
                    resultMsg = addyResponse.reason;
                }
            }
            else
            {
                result = VerificationResult.ConnectionError;
                resultMsg = response.StatusDescription;
            }

            location.StandardizeAttemptedServiceType = "Addy";
            location.StandardizeAttemptedDateTime = RockDateTime.Now;

            location.GeocodeAttemptedServiceType = "Addy";
            location.GeocodeAttemptedDateTime = RockDateTime.Now;
            return result;
        }

        /// <summary>
        /// Builds a REST request 
        /// </summary>
        /// <param name="streetAddress"></param>
        /// <param name="apiKey"></param>
        /// <param name="apiSecret"></param>
        /// <returns></returns>
        private static IRestRequest BuildRequest(string streetAddress, string apiKey, string apiSecret)
        {
            var request = new RestRequest("validation", Method.GET);
            request.RequestFormat = DataFormat.Json;
            request.AddHeader("Accept", "application/json");
            request.AddParameter("address", streetAddress);
            request.AddParameter("key", apiKey);
            request.AddParameter("secret", apiSecret);

            return request;
        }

        /// <summary>
        /// Updates a Rock location to match a Addy AddressDetail
        /// </summary>
        /// <param name="location">The Rock location to be modified</param>
        /// <param name="address">The Addy AddressDetail to copy the data from</param>
        /// <returns>Whether the Location was succesfully geocoded</returns>
        public bool UpdateLocation(Rock.Model.Location location, AddressDetail address)
        {
            location.Street1 = address.address1;
            location.Street2 = address.address2;
            location.City = address.city;
            location.State = String.Empty;
            location.PostalCode = address.postcode;
            location.StandardizedDateTime = RockDateTime.Now;

            // If AddressDetail has geocoding data set it on Location
            if (address.x.IsNotNullOrWhiteSpace() && address.y.IsNotNullOrWhiteSpace())
            {
                bool setLocationResult = location.SetLocationPointFromLatLong(Convert.ToDouble(address.y), Convert.ToDouble(address.x));
                if (setLocationResult)
                {
                    location.GeocodedDateTime = RockDateTime.Now;
                }
                return setLocationResult;
            }

            return false;
        }

#pragma warning disable

        /// <summary>
        /// Address metadata and details. See: https://www.addy.co.nz/address-details-api
        /// </summary>
        public class AddressDetail
        {
            /// <summary>
            /// Unique Addy identifier
            /// </summary>
            public int id { get; set; }

            /// <summary>
            /// Unique NZ Post identifier.This property can be null for a non-mail deliverable address
            /// </summary>
            public int dpid { get; set; }

            /// <summary>
            /// Unique Land Information New Zealand(LINZ) identifier
            /// </summary>
            public int linzid { get; set; }

            /// <summary>
            /// Unique Parcel identifier
            /// </summary>
            public int parcelid { get; set; }

            /// <summary>
            /// Unique Statistics New Zealand(Stats NZ) identifier to match census data
            /// </summary>
            public int meshblock { get; set; }

            /// <summary>
            /// Street number.Street number will be "80" in case of "80A Queen Street"
            /// </summary>
            public string number { get; set; }

            /// <summary>
            /// Rural delivery number (postal only) for rural addresses
            /// </summary>
            public string rdnumber { get; set; }

            /// <summary>
            /// Street alpha e.g. "A" in the case of "80A Queen Street"
            /// </summary>
            public string alpha { get; set; }

            /// <summary>
            /// Type of unit e.g. "FLAT" in "FLAT 3, 80 Queen Street"
            /// </summary>
            public string unittype { get; set; }

            /// <summary>
            /// Unit number e.g. "3" in "FLAT 3, 80 Queen Street"
            /// </summary>
            public string unitnumber { get; set; }

            /// <summary>
            /// Floor number e.g. "Floor 5" in "Floor 5, 80 Queen Street"
            /// </summary>
            public string floor { get; set; }

            /// <summary>
            /// Street name.The name of the street / road, including prefix
            /// </summary>
            public string street { get; set; }

            /// <summary>
            /// Suburb name string (max 60)
            /// </summary>
            public string suburb { get; set; }

            /// <summary>
            /// Name of the town or city provided by Land Information New Zealand (LINZ) (max 60)
            /// </summary>
            public string city { get; set; }

            /// <summary>
            /// Name of the town or city provided by NZ Post (max 60)
            /// </summary>
            public string mailtown { get; set; }

            /// <summary>
            /// Territorial authority of the address (max 20)
            /// </summary>
            public string territory { get; set; }

            /// <summary>
            /// Regional authority of the address.See regions of NZ (max 20)
            /// </summary>
            public string region { get; set; }

            /// <summary>
            /// NZ Post code used for defining an area string (max 4)
            /// </summary>
            public string postcode { get; set; }

            /// <summary>
            /// Name of the building string (max 60)
            /// </summary>
            public string building { get; set; }

            /// <summary>
            /// Full display name or label for an address (max 90)
            /// </summary>
            public string full { get; set; }

            /// <summary>
            /// One line address display name (max 70)
            /// </summary>
            public string displayline { get; set; }

            /// <summary>
            /// Line 1 in a 4 address field form string (max 60)
            /// </summary>
            public string address1 { get; set; }

            /// <summary>
            /// Line 2 in a 4 address field form string (max 60)
            /// </summary>
            public string address2 { get; set; }

            /// <summary>
            /// Line 3 in a 4 address field form string (max 60)
            /// </summary>
            public string address3 { get; set; }

            /// <summary>
            /// Line 4 in a 4 address field form string (max 60)
            /// </summary>
            public string address4 { get; set; }

            /// <summary>
            /// Address Type (Urban, Rural, PostBox, NonPostal)
            /// </summary>
            public string type { get; set; }

            /// <summary>
            /// The PO Box number for PO Box addresses
            /// </summary>
            public string boxbagnumber { get; set; }

            /// <summary>
            /// NZ Post outlet or agency where the PO Box is located
            /// </summary>
            public string boxbaglobby { get; set; }

            /// <summary>
            /// Longitude coordinates in WGS84 format (max 20)
            /// </summary>
            public string x { get; set; }

            /// <summary>
            /// Latitude coordinates in WGS84 format (max 20)
            /// </summary>
            public string y { get; set; }

            /// <summary>
            /// Last updated date
            /// </summary>
            public string modified { get; set; }

            /// <summary>
            /// True/False to indicate if the address was sourced from PAF (or LINZ = false)
            /// </summary>
            public bool paf { get; set; }

            /// <summary>
            /// True/False to indicate if the address was deleted from the source (PAF or LINZ)
            /// </summary>
            public bool deleted { get; set; }
        }

        /// <summary>
        /// Address reference
        /// </summary>
        public class AddressReference
        {
            /// <summary>
            /// Unique identifier for the address
            /// </summary>
            public int id { get; set; }

            /// <summary>
            /// Full display name of the address
            /// </summary>
            public string a { get; set; }
        }

        /// <summary>
        /// Address Validation Result
        /// </summary>
        public class AddressVerificationResult
        {
            /// <summary>
            /// The matched address.
            /// </summary>
            public AddressDetail address { get; set; }

            /// <summary>
            /// Alternative address matches.
            /// </summary>
            public List<AddressReference> alternatives { get; set; }

            /// <summary>
            /// The reason for the match result.
            /// </summary>
            public string reason { get; set; }

            /// <summary>
            /// True if a prefix was found.
            /// </summary>
            public bool foundPrefix { get; set; }

            /// <summary>
            /// Found a prefix, such as "Front Door" or "Rear Unit"
            /// </summary>
            public string prefix { get; set; }
        }

#pragma warning restore

    }
}
