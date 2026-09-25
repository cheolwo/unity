using System;
using System.Collections.Generic;

namespace Ssalddel.Unity.Runtime.WorldMap
{
    [Serializable]
    public sealed class 세계육지Polygon사본
    {
        public string schemaVersion,catalogHash;
        public int countryCount,polygonCount,holeCount,maxCellDegrees;
        // longitude,latitude 교대 배열. 중복 꼭짓점을 공유한다.
        public double[] coordinates;
        public int[] indices;
        public void Validate(string expectedCatalogHash)
        {
            if(schemaVersion!="world-land-polygons.v1"||catalogHash!=expectedCatalogHash||countryCount!=177||polygonCount<177||maxCellDegrees!=1)
                throw new InvalidOperationException("WorldLandPolygonContractInvalid");
            if(coordinates==null||coordinates.Length<6||coordinates.Length%2!=0||coordinates.Length>1000000||indices==null||indices.Length==0||indices.Length%3!=0||indices.Length>1500000)
                throw new InvalidOperationException("WorldLandPolygonSizeInvalid");
            for(int i=0;i<coordinates.Length;i+=2)
                if(!(coordinates[i]>=-180&&coordinates[i]<=180&&coordinates[i+1]>=-90&&coordinates[i+1]<=90))
                    throw new InvalidOperationException("WorldLandPolygonCoordinateInvalid");
            foreach(var index in indices)if(index<0||index>=coordinates.Length/2)
                throw new InvalidOperationException("WorldLandPolygonIndexInvalid");
            for(int i=0;i<indices.Length;i+=3)
            {
                double minLon=180,maxLon=-180,minLat=90,maxLat=-90;
                for(int j=0;j<3;j++){int p=indices[i+j]*2;minLon=Math.Min(minLon,coordinates[p]);maxLon=Math.Max(maxLon,coordinates[p]);minLat=Math.Min(minLat,coordinates[p+1]);maxLat=Math.Max(maxLat,coordinates[p+1]);}
                if(maxLon-minLon>1.000001||maxLat-minLat>1.000001)throw new InvalidOperationException("WorldLandPolygonLongEdgeInvalid");
            }
        }
    }
    [Serializable]
    public sealed class 세계지구본Source
    {
        public string sourceId = string.Empty;
        public string datasetId = string.Empty;
        public string sourceName = string.Empty;
        public string sourceUrl = string.Empty;
        public string sourceRevision = string.Empty;
        public string licenseCode = string.Empty;
        public string contentHashSha256 = string.Empty;
        public long contentLength;
        public string coordinateReferenceSystem = string.Empty;
        public string attribution = string.Empty;
    }

    [Serializable]
    public sealed class 세계위경도Point
    {
        public double longitude;
        public double latitude;
    }

    [Serializable]
    public sealed class 세계국가경계Ring
    {
        public int polygonIndex;
        public bool isHole;
        public 세계위경도Point[] points = Array.Empty<세계위경도Point>();
    }

    [Serializable]
    public sealed class 세계국가경계Bounds
    {
        public double minLatitude;
        public double maxLatitude;
        public double minLongitude;
        public double maxLongitude;
    }

    [Serializable]
    public sealed class 세계국가관찰자료
    {
        public string stableId = string.Empty;
        public string naturalEarthId = string.Empty;
        public string countryCode = string.Empty;
        public string iso2 = string.Empty;
        public string iso3 = string.Empty;
        public string naturalEarthCode = string.Empty;
        public string displayName = string.Empty;
        public string englishName = string.Empty;
        public string formalName = string.Empty;
        public string continent = string.Empty;
        public string region = string.Empty;
        public string subregion = string.Empty;
        public string capitalCity = string.Empty;
        public double centerLatitude;
        public double centerLongitude;
        public long populationValue = -1;
        public string populationYear = string.Empty;
        public string populationSourceId = string.Empty;
        public string dataAvailabilityCode = string.Empty;
        public string maximumDetailCode = string.Empty;
        public 세계국가경계Bounds bounds = new();
        public 세계국가경계Ring[] rings = Array.Empty<세계국가경계Ring>();

        public bool Contains(double latitude, double longitude)
        {
            if (latitude < bounds.minLatitude || latitude > bounds.maxLatitude)
                return false;

            var longitudeSpan = bounds.maxLongitude - bounds.minLongitude;
            if (longitudeSpan < 180d
                && (longitude < bounds.minLongitude || longitude > bounds.maxLongitude))
                return false;

            for (var outerIndex = 0; outerIndex < rings.Length; outerIndex++)
            {
                var outer = rings[outerIndex];
                if (outer.isHole || !PointInRing(outer, latitude, longitude)) continue;
                var excludedByHole = false;
                for (var holeIndex = 0; holeIndex < rings.Length; holeIndex++)
                {
                    var hole = rings[holeIndex];
                    if (!hole.isHole || hole.polygonIndex != outer.polygonIndex) continue;
                    if (!PointInRing(hole, latitude, longitude)) continue;
                    excludedByHole = true;
                    break;
                }
                if (!excludedByHole) return true;
            }
            return false;
        }

        private static bool PointInRing(
            세계국가경계Ring ring,
            double latitude,
            double longitude)
        {
            if (ring?.points == null || ring.points.Length < 4) return false;
            var unwrapped = new double[ring.points.Length];
            unwrapped[0] = NormalizeDelta(ring.points[0].longitude - longitude);
            for (var index = 1; index < ring.points.Length; index++)
            {
                var value = NormalizeDelta(ring.points[index].longitude - longitude);
                while (value - unwrapped[index - 1] > 180d) value -= 360d;
                while (value - unwrapped[index - 1] < -180d) value += 360d;
                unwrapped[index] = value;
            }

            var inside = false;
            for (int current = 0, previous = ring.points.Length - 1;
                 current < ring.points.Length;
                 previous = current++)
            {
                var currentLatitude = ring.points[current].latitude;
                var previousLatitude = ring.points[previous].latitude;
                if ((currentLatitude > latitude) == (previousLatitude > latitude)) continue;
                var crossing = (unwrapped[previous] - unwrapped[current])
                    * (latitude - currentLatitude)
                    / (previousLatitude - currentLatitude)
                    + unwrapped[current];
                if (0d < crossing) inside = !inside;
            }
            return inside;
        }

        private static double NormalizeDelta(double value)
        {
            while (value > 180d) value -= 360d;
            while (value < -180d) value += 360d;
            return value;
        }
    }

    [Serializable]
    public sealed class 세계지구본Catalog
    {
        public string schemaVersion = string.Empty;
        public string revision = string.Empty;
        public string generatedAtUtc = string.Empty;
        public bool distributionApproved;
        public bool observationPresentationOnly;
        public string boundaryNotice = string.Empty;
        public 세계지구본Source[] sources = Array.Empty<세계지구본Source>();
        public 세계국가관찰자료[] countries = Array.Empty<세계국가관찰자료>();

        public void Validate()
        {
            if (schemaVersion != "world-globe-country-catalog.v1")
                throw new InvalidOperationException("WorldGlobeCatalogSchemaVersionUnsupported");
            if (string.IsNullOrWhiteSpace(revision)
                || !DateTimeOffset.TryParse(generatedAtUtc, out _))
                throw new InvalidOperationException("WorldGlobeCatalogRevisionInvalid");
            if (!distributionApproved || !observationPresentationOnly)
                throw new InvalidOperationException("WorldGlobeCatalogDistributionBoundaryInvalid");
            if (string.IsNullOrWhiteSpace(boundaryNotice))
                throw new InvalidOperationException("WorldGlobeCatalogBoundaryNoticeMissing");
            if (sources == null || sources.Length < 3)
                throw new InvalidOperationException("WorldGlobeCatalogSourcesMissing");
            foreach (var source in sources)
            {
                if (string.IsNullOrWhiteSpace(source.sourceId)
                    || string.IsNullOrWhiteSpace(source.datasetId)
                    || string.IsNullOrWhiteSpace(source.sourceName)
                    || !Uri.TryCreate(source.sourceUrl, UriKind.Absolute, out var sourceUri)
                    || sourceUri.Scheme != Uri.UriSchemeHttps
                    || source.contentLength <= 0
                    || !IsSha256(source.contentHashSha256)
                    || string.IsNullOrWhiteSpace(source.licenseCode)
                    || string.IsNullOrWhiteSpace(source.attribution))
                    throw new InvalidOperationException("WorldGlobeCatalogSourceInvalid");
            }
            if (countries == null || countries.Length < 170)
                throw new InvalidOperationException("WorldGlobeCatalogCountryCountInvalid");

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var country in countries)
            {
                if (country == null
                    || string.IsNullOrWhiteSpace(country.stableId)
                    || string.IsNullOrWhiteSpace(country.countryCode)
                    || string.IsNullOrWhiteSpace(country.displayName)
                    || !ids.Add(country.stableId)
                    || country.centerLatitude is < -90d or > 90d
                    || country.centerLongitude is < -180d or > 180d
                    || country.rings == null
                    || country.rings.Length == 0)
                    throw new InvalidOperationException("WorldGlobeCatalogCountryInvalid");

                foreach (var ring in country.rings)
                {
                    if (ring?.points == null || ring.points.Length < 4)
                        throw new InvalidOperationException("WorldGlobeCatalogRingInvalid");
                    foreach (var point in ring.points)
                    {
                        if (point == null
                            || point.latitude is < -90d or > 90d
                            || point.longitude is < -180d or > 180d)
                            throw new InvalidOperationException("WorldGlobeCatalogPointInvalid");
                    }
                }
            }
        }

        public 세계국가관찰자료 FindByStableId(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId)) return null;
            foreach (var country in countries)
                if (string.Equals(country.stableId, stableId, StringComparison.Ordinal))
                    return country;
            return null;
        }

        public 세계국가관찰자료 FindAt(double latitude, double longitude)
        {
            if (latitude is < -90d or > 90d || longitude is < -180d or > 180d)
                return null;
            foreach (var country in countries)
                if (country.Contains(latitude, longitude)) return country;
            return null;
        }

        private static bool IsSha256(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 64) return false;
            foreach (var character in value)
                if (!Uri.IsHexDigit(character)) return false;
            return true;
        }
    }

    public readonly struct 세계지구구면좌표
    {
        public 세계지구구면좌표(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public static 세계지구구면좌표 FromLatitudeLongitude(
            double latitude,
            double longitude)
        {
            var latitudeRadians = latitude * Math.PI / 180d;
            var longitudeRadians = longitude * Math.PI / 180d;
            var cosine = Math.Cos(latitudeRadians);
            return new 세계지구구면좌표(
                cosine * Math.Sin(longitudeRadians),
                Math.Sin(latitudeRadians),
                cosine * Math.Cos(longitudeRadians));
        }

        public (double Latitude, double Longitude) ToLatitudeLongitude()
        {
            var magnitude = Math.Sqrt(X * X + Y * Y + Z * Z);
            if (magnitude <= double.Epsilon)
                throw new InvalidOperationException("WorldGlobeDirectionZero");
            var latitude = Math.Asin(Y / magnitude) * 180d / Math.PI;
            var longitude = Math.Atan2(X, Z) * 180d / Math.PI;
            return (latitude, longitude);
        }
    }
}
