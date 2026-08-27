namespace CvarcLogger.Core.Geo;

/// <summary>Derives an ARRL/RAC contest Section from a station's State and County, as returned by an
/// online callsign lookup (see LookupCoordinator). Most US states and Canadian provinces map 1:1 to a
/// section; larger states are split further by county — those splits are best-effort, not guaranteed
/// exhaustive (Texas and Florida especially: 254 and 67 counties is too many to reproduce reliably from
/// memory). An unmapped county in a split state deliberately returns null rather than guessing, same as
/// any state/county this resolver doesn't recognize at all. Ontario (split into GTA/ONE/ONN/ONS) and the
/// Canadian territories are omitted for the same reason. This data has no authoritative source file
/// backing it in this repo — spot-check against ARRL's official section map before relying on it for
/// contest submission.</summary>
public static class ArrlSectionResolver
{
    // States (and DC/territories/non-split VE provinces) that map to exactly one section.
    private static readonly Dictionary<string, string> SingleSectionStates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AL"] = "AL", ["AK"] = "AK", ["AZ"] = "AZ", ["AR"] = "AR", ["CO"] = "CO", ["CT"] = "CT",
        ["DE"] = "DE", ["DC"] = "MDC", ["MD"] = "MDC", ["GA"] = "GA", ["HI"] = "PAC", ["ID"] = "ID", ["IL"] = "IL",
        ["IN"] = "IN", ["IA"] = "IA", ["KS"] = "KS", ["KY"] = "KY", ["LA"] = "LA", ["ME"] = "ME",
        ["MI"] = "MI", ["MN"] = "MN", ["MS"] = "MS", ["MO"] = "MO", ["MT"] = "MT", ["NE"] = "NE",
        ["NV"] = "NV", ["NH"] = "NH", ["NM"] = "NM", ["NC"] = "NC", ["ND"] = "ND", ["OH"] = "OH",
        ["OK"] = "OK", ["OR"] = "OR", ["PR"] = "PR", ["RI"] = "RI", ["SC"] = "SC", ["SD"] = "SD",
        ["TN"] = "TN", ["UT"] = "UT", ["VT"] = "VT", ["VA"] = "VA", ["VI"] = "VI", ["WV"] = "WV",
        ["WY"] = "WY",
        ["AB"] = "AB", ["BC"] = "BC", ["MB"] = "MB", ["SK"] = "SK", ["QC"] = "QC", ["NL"] = "NL",
        ["NB"] = "MAR", ["NS"] = "MAR", ["PE"] = "MAR",
    };

    private static readonly Dictionary<string, Dictionary<string, string>> SplitStateCounties =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["CA"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Alameda"] = "EB", ["Contra Costa"] = "EB",
                ["Los Angeles"] = "LAX",
                ["Orange"] = "ORG",
                ["San Diego"] = "SDG", ["Imperial"] = "SDG",
                ["San Francisco"] = "SF", ["Marin"] = "SF", ["San Mateo"] = "SF",
                ["Monterey"] = "SCV", ["San Benito"] = "SCV", ["Santa Clara"] = "SCV", ["Santa Cruz"] = "SCV",
                ["San Luis Obispo"] = "SB", ["Santa Barbara"] = "SB", ["Ventura"] = "SB",
                ["Calaveras"] = "SJV", ["Fresno"] = "SJV", ["Kern"] = "SJV", ["Kings"] = "SJV",
                ["Madera"] = "SJV", ["Mariposa"] = "SJV", ["Merced"] = "SJV", ["San Joaquin"] = "SJV",
                ["Stanislaus"] = "SJV", ["Tulare"] = "SJV", ["Tuolumne"] = "SJV",
                ["Alpine"] = "SV", ["Amador"] = "SV", ["Butte"] = "SV", ["Colusa"] = "SV",
                ["El Dorado"] = "SV", ["Glenn"] = "SV", ["Inyo"] = "SV", ["Lassen"] = "SV",
                ["Modoc"] = "SV", ["Mono"] = "SV", ["Nevada"] = "SV", ["Placer"] = "SV",
                ["Plumas"] = "SV", ["Sacramento"] = "SV", ["Shasta"] = "SV", ["Sierra"] = "SV",
                ["Siskiyou"] = "SV", ["Sutter"] = "SV", ["Tehama"] = "SV", ["Yolo"] = "SV", ["Yuba"] = "SV",
            },
            ["NY"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Bronx"] = "NLI", ["Kings"] = "NLI", ["Nassau"] = "NLI", ["New York"] = "NLI",
                ["Queens"] = "NLI", ["Richmond"] = "NLI", ["Suffolk"] = "NLI",
                ["Franklin"] = "NNY", ["Jefferson"] = "NNY", ["Lewis"] = "NNY",
                ["St. Lawrence"] = "NNY", ["St Lawrence"] = "NNY",
                ["Albany"] = "ENY", ["Clinton"] = "ENY", ["Columbia"] = "ENY", ["Delaware"] = "ENY",
                ["Dutchess"] = "ENY", ["Essex"] = "ENY", ["Fulton"] = "ENY", ["Greene"] = "ENY",
                ["Hamilton"] = "ENY", ["Montgomery"] = "ENY", ["Orange"] = "ENY", ["Otsego"] = "ENY",
                ["Putnam"] = "ENY", ["Rensselaer"] = "ENY", ["Rockland"] = "ENY", ["Saratoga"] = "ENY",
                ["Schenectady"] = "ENY", ["Schoharie"] = "ENY", ["Sullivan"] = "ENY", ["Ulster"] = "ENY",
                ["Warren"] = "ENY", ["Washington"] = "ENY", ["Westchester"] = "ENY",
                ["Allegany"] = "WNY", ["Broome"] = "WNY", ["Cattaraugus"] = "WNY", ["Cayuga"] = "WNY",
                ["Chautauqua"] = "WNY", ["Chemung"] = "WNY", ["Chenango"] = "WNY", ["Cortland"] = "WNY",
                ["Erie"] = "WNY", ["Genesee"] = "WNY", ["Livingston"] = "WNY", ["Madison"] = "WNY",
                ["Monroe"] = "WNY", ["Niagara"] = "WNY", ["Oneida"] = "WNY", ["Onondaga"] = "WNY",
                ["Ontario"] = "WNY", ["Orleans"] = "WNY", ["Oswego"] = "WNY", ["Schuyler"] = "WNY",
                ["Seneca"] = "WNY", ["Steuben"] = "WNY", ["Tioga"] = "WNY", ["Tompkins"] = "WNY",
                ["Wayne"] = "WNY", ["Wyoming"] = "WNY", ["Yates"] = "WNY",
            },
            ["PA"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Adams"] = "EPA", ["Berks"] = "EPA", ["Bucks"] = "EPA", ["Carbon"] = "EPA",
                ["Chester"] = "EPA", ["Columbia"] = "EPA", ["Cumberland"] = "EPA", ["Dauphin"] = "EPA",
                ["Delaware"] = "EPA", ["Lackawanna"] = "EPA", ["Lancaster"] = "EPA", ["Lebanon"] = "EPA",
                ["Lehigh"] = "EPA", ["Luzerne"] = "EPA", ["Monroe"] = "EPA", ["Montgomery"] = "EPA",
                ["Northampton"] = "EPA", ["Northumberland"] = "EPA", ["Philadelphia"] = "EPA",
                ["Pike"] = "EPA", ["Schuylkill"] = "EPA", ["Susquehanna"] = "EPA", ["Wayne"] = "EPA",
                ["Wyoming"] = "EPA", ["York"] = "EPA",
                ["Allegheny"] = "WPA", ["Armstrong"] = "WPA", ["Beaver"] = "WPA", ["Bedford"] = "WPA",
                ["Blair"] = "WPA", ["Bradford"] = "WPA", ["Butler"] = "WPA", ["Cambria"] = "WPA",
                ["Cameron"] = "WPA", ["Centre"] = "WPA", ["Clarion"] = "WPA", ["Clearfield"] = "WPA",
                ["Clinton"] = "WPA", ["Crawford"] = "WPA", ["Elk"] = "WPA", ["Erie"] = "WPA",
                ["Fayette"] = "WPA", ["Forest"] = "WPA", ["Franklin"] = "WPA", ["Fulton"] = "WPA",
                ["Greene"] = "WPA", ["Huntingdon"] = "WPA", ["Indiana"] = "WPA", ["Jefferson"] = "WPA",
                ["Juniata"] = "WPA", ["Lawrence"] = "WPA", ["Lycoming"] = "WPA", ["McKean"] = "WPA",
                ["Mercer"] = "WPA", ["Mifflin"] = "WPA", ["Montour"] = "WPA", ["Perry"] = "WPA",
                ["Potter"] = "WPA", ["Snyder"] = "WPA", ["Somerset"] = "WPA", ["Sullivan"] = "WPA",
                ["Tioga"] = "WPA", ["Union"] = "WPA", ["Venango"] = "WPA", ["Warren"] = "WPA",
                ["Washington"] = "WPA", ["Westmoreland"] = "WPA",
            },
            ["MA"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Barnstable"] = "EMA", ["Bristol"] = "EMA", ["Dukes"] = "EMA", ["Essex"] = "EMA",
                ["Middlesex"] = "EMA", ["Nantucket"] = "EMA", ["Norfolk"] = "EMA", ["Plymouth"] = "EMA",
                ["Suffolk"] = "EMA",
                ["Berkshire"] = "WMA", ["Franklin"] = "WMA", ["Hampden"] = "WMA", ["Hampshire"] = "WMA",
                ["Worcester"] = "WMA",
            },
            ["NJ"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Bergen"] = "NNJ", ["Essex"] = "NNJ", ["Hudson"] = "NNJ", ["Hunterdon"] = "NNJ",
                ["Middlesex"] = "NNJ", ["Morris"] = "NNJ", ["Passaic"] = "NNJ", ["Somerset"] = "NNJ",
                ["Sussex"] = "NNJ", ["Union"] = "NNJ", ["Warren"] = "NNJ",
                ["Atlantic"] = "SNJ", ["Burlington"] = "SNJ", ["Camden"] = "SNJ", ["Cape May"] = "SNJ",
                ["Cumberland"] = "SNJ", ["Gloucester"] = "SNJ", ["Mercer"] = "SNJ", ["Monmouth"] = "SNJ",
                ["Ocean"] = "SNJ", ["Salem"] = "SNJ",
            },
            ["WA"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Adams"] = "EWA", ["Asotin"] = "EWA", ["Benton"] = "EWA", ["Chelan"] = "EWA",
                ["Columbia"] = "EWA", ["Douglas"] = "EWA", ["Ferry"] = "EWA", ["Franklin"] = "EWA",
                ["Garfield"] = "EWA", ["Grant"] = "EWA", ["Kittitas"] = "EWA", ["Klickitat"] = "EWA",
                ["Lincoln"] = "EWA", ["Okanogan"] = "EWA", ["Pend Oreille"] = "EWA", ["Spokane"] = "EWA",
                ["Stevens"] = "EWA", ["Walla Walla"] = "EWA", ["Whitman"] = "EWA", ["Yakima"] = "EWA",
                ["Clallam"] = "WWA", ["Clark"] = "WWA", ["Cowlitz"] = "WWA", ["Grays Harbor"] = "WWA",
                ["Island"] = "WWA", ["Jefferson"] = "WWA", ["King"] = "WWA", ["Kitsap"] = "WWA",
                ["Lewis"] = "WWA", ["Mason"] = "WWA", ["Pacific"] = "WWA", ["Pierce"] = "WWA",
                ["San Juan"] = "WWA", ["Skagit"] = "WWA", ["Skamania"] = "WWA", ["Snohomish"] = "WWA",
                ["Thurston"] = "WWA", ["Wahkiakum"] = "WWA", ["Whatcom"] = "WWA",
            },
            // Best-effort only -- 254 counties, so only major population centers are listed. Every
            // other TX county returns null (no auto-fill) rather than a guess.
            ["TX"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Collin"] = "NTX", ["Cooke"] = "NTX", ["Dallas"] = "NTX", ["Denton"] = "NTX",
                ["Ellis"] = "NTX", ["Fannin"] = "NTX", ["Grayson"] = "NTX", ["Hood"] = "NTX",
                ["Hunt"] = "NTX", ["Johnson"] = "NTX", ["Kaufman"] = "NTX", ["Lamar"] = "NTX",
                ["Navarro"] = "NTX", ["Parker"] = "NTX", ["Rockwall"] = "NTX", ["Tarrant"] = "NTX",
                ["Wichita"] = "NTX", ["Wise"] = "NTX",
                ["El Paso"] = "WTX", ["Ector"] = "WTX", ["Midland"] = "WTX", ["Lubbock"] = "WTX",
                ["Potter"] = "WTX", ["Randall"] = "WTX", ["Taylor"] = "WTX", ["Tom Green"] = "WTX",
                ["Harris"] = "STX", ["Bexar"] = "STX", ["Travis"] = "STX", ["Williamson"] = "STX",
                ["Hidalgo"] = "STX", ["Cameron"] = "STX", ["Nueces"] = "STX", ["Webb"] = "STX",
                ["Galveston"] = "STX", ["Fort Bend"] = "STX", ["Montgomery"] = "STX", ["Brazoria"] = "STX",
                ["Comal"] = "STX", ["Guadalupe"] = "STX",
            },
            // Best-effort only -- same caveat as TX, 67 counties.
            ["FL"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Duval"] = "NFL", ["Leon"] = "NFL", ["Escambia"] = "NFL", ["Alachua"] = "NFL",
                ["Santa Rosa"] = "NFL", ["Okaloosa"] = "NFL", ["Bay"] = "NFL", ["Clay"] = "NFL",
                ["St. Johns"] = "NFL", ["St Johns"] = "NFL", ["Nassau"] = "NFL",
                ["Hillsborough"] = "WCF", ["Pinellas"] = "WCF", ["Pasco"] = "WCF", ["Polk"] = "WCF",
                ["Manatee"] = "WCF", ["Sarasota"] = "WCF", ["Hernando"] = "WCF", ["Citrus"] = "WCF",
                ["Miami-Dade"] = "SFL", ["Dade"] = "SFL", ["Broward"] = "SFL", ["Palm Beach"] = "SFL",
                ["Orange"] = "SFL", ["Seminole"] = "SFL", ["Osceola"] = "SFL", ["Brevard"] = "SFL",
                ["Volusia"] = "SFL", ["Lee"] = "SFL", ["Collier"] = "SFL", ["Martin"] = "SFL",
                ["St. Lucie"] = "SFL", ["St Lucie"] = "SFL", ["Indian River"] = "SFL", ["Monroe"] = "SFL",
            },
        };

    public static string? Resolve(string? state, string? county)
    {
        if (string.IsNullOrWhiteSpace(state)) return null;
        string trimmedState = state.Trim();

        if (SplitStateCounties.TryGetValue(trimmedState, out var counties))
        {
            string? normalizedCounty = NormalizeCounty(county);
            return normalizedCounty is not null && counties.TryGetValue(normalizedCounty, out var section)
                ? section
                : null;
        }

        return SingleSectionStates.TryGetValue(trimmedState, out var singleSection) ? singleSection : null;
    }

    /// <summary>Strips a trailing "County"/"Parish" word -- lookup services report county names
    /// inconsistently ("Los Angeles" vs "Los Angeles County").</summary>
    private static string? NormalizeCounty(string? county)
    {
        if (string.IsNullOrWhiteSpace(county)) return null;
        string trimmed = county.Trim();
        foreach (var suffix in new[] { " County", " Parish" })
        {
            if (trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return trimmed[..^suffix.Length].Trim();
        }
        return trimmed;
    }
}
