# Rock Addy Location Service

## Intro
This is a location service for [Rock](http://rockrms.com) that verifies, standardises and geocodes New Zealand (NZ) addresses using the [Addy](http://mappify.io) API. The generous 500 free requests per month on the free signup option will meet most small churches needs.

This plugin is available on Github to help the New Zealand churches using Rock RMS.  The repository includes the C# source for use with the [Rockit SDK](http://www.rockrms.com/Rock/Developer). To download the latest release of the plugin in .dll format for quick install into the Rock bin folder click [here](https://github.com/stewmystre/Addy/releases/latest). Checkout the [wiki](https://github.com/stewmystre/Addy/wiki) for more detailed install instructions.

## A Quick Explanation
This location service will pass the values (if any are present) of the address line 1, address line 2, city, state, and postal code fields from Rock to the Addy Address Validation API service. If values are present in the response, it will either:
1. confirm verification and replace the address values stored in Rock with the standardised response values, including geocode coordinates, or
2. deny verification, due to multiple matches, and instead provide the first few listed matches provided by Addy. If this happens, check the recommended match details provided and if suitable update the address to one of the options and verify again.

The Rock Data Field is updated to match the Addy Address Details Metadata Properties as per the table:

Rock Location Data Field | Addy Address Details Property
---- | ----
Street1| address1
Street2 | address2
City | city
PostalCode | postcode

## Addy Data
Addy uses New Zealand addresses sourced from the PAF, GeoPAF and LINZ, more [info](https://www.addy.co.nz/faq-where-does-address-lookup-data-come-from).

## Contribute
If anything looks broken or you think of an improvement please flag up an issue.

## Thanks
Thanks to [Porirua Elim](https://www.porirua.elim.org.nz/) for sponsoring this plugin after seeing the work done for [Hope Central](https://hopecentral.melbourne/) in Australia whose [mappify.io](https://github.com/hopecentral/mappify.io) plugin was used as the base for this one.
Thanks to [Bricks and Mortar Studio](https://bricksandmortarstudio.com/) whose [IdealPostcodes](https://github.com/BricksandMortar/IdealPostcodes) plugin was where internationalisation of Rock got a start.
Thanks to the [Spark Development Network](https://sparkdevnetwork.org/) for creating [Rock](https://github.com/SparkDevNetwork/Rock) and making it so accessible.

This project is licensed under the [Apache 2.0 License](http://www.apache.org/licenses/LICENSE-2.0.html).
