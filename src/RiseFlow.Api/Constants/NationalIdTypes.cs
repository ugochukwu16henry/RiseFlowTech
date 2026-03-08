namespace RiseFlow.Api.Constants;

/// <summary>
/// National identification types for student tracking across African ministries (NIMC/NIN Nigeria, etc.).
/// Used with Student.NationalIdType and Student.NationalIdNumber (or Student.NIN for Nigeria).
/// </summary>
public static class NationalIdTypes
{
    public const string NigeriaNIN = "NIN";
    public const string GhanaCard = "GHANA_CARD";
    public const string KenyaId = "KENYA_ID";
    public const string SouthAfricaId = "RSA_ID";
    public const string TanzaniaId = "TZ_ID";
    public const string UgandaId = "UGANDA_ID";
    public const string SenegalCnib = "SENEGAL_CNIB";
    public const string Other = "OTHER";

    public static readonly IReadOnlyDictionary<string, string> DisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [NigeriaNIN] = "National Identification Number (NIN)",
        [GhanaCard] = "Ghana Card (Ghana Card PIN)",
        [KenyaId] = "Kenya National ID",
        [SouthAfricaId] = "South African ID Number",
        [TanzaniaId] = "Tanzania NIDA",
        [UgandaId] = "Uganda National ID",
        [SenegalCnib] = "Senegal CNIB",
        [Other] = "Other",
    };
}
