namespace OpenEvent.Data.Models.Address
{
#pragma warning disable CS1591
    public class SearchAddressResponse
    {
        public SearchAddressResult[] Results { get; set; }
        public SearchAddressSummary Summary { get; set; }
    }

    public class SearchAddressSummary
    {
        
    }

    public class SearchAddressResult
    {
        public SearchResultAddress Address { get; set; }
        public CoordinateAbbreviated Position { get; set; }
    }

    public class CoordinateAbbreviated
    {
        public double Lat { get; set; }
        public double Lon { get; set; }
    }

    public class SearchResultAddress
    {
    }
#pragma warning restore CS1591
}