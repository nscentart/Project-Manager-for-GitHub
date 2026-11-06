using System;
using System.Globalization;
using System.Windows.Forms;

public static class AppLicense {
    public const string Program = "ProjectManager";
    public const string Contact = "info@nscent.art";
    public static string Shop(string language) {
        switch (language) {
            case "en": return "www.nscent.art/en/software";
            case "fr": return "www.nscent.art/fr/software";
            case "es": return "www.nscent.art/es/software";
            case "de": return "www.nscent.art/de/software";
            default: return "www.nscent.art/software";
        }
    }
    public static string Sale { get { return Language.T("license.commercial_license_sale_soon_meanwhile", "shop", Shop(Language.Code), "contact", Contact); } }
    public static string Limits { get { return Language.T("license.program_works_same_without_license"); } }

    public static string[] Keys = License.PublicKeys;

    public static LicenseResult Now(string folder) { return License.Load(folder, Program, Keys, License.Revoked); }
    public static LicenseResult Check(string text) { return License.Check(text, Program, Keys, License.Revoked); }

    public static string LicenseCaption(LicenseResult e) { return e.Valid ? Language.T("license.commercial_license", "holder", e.Holder) : Language.T("license.personal_license"); }

    public static string Data(LicenseResult e) {
        DateTime d;
        return DateTime.TryParseExact(e.Data, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d) ? d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : e.Data;
    }

    public static string Problem(LicenseResult e, string which) {
        switch (e.State) {
            case LicenseState.NotValid: return Language.T("license.not_valid_may_been_copied", "which", which);
            case LicenseState.OtherProgram: return Language.T("license.license_valid_one_program_only", "which", which, "program", e.Program);
            case LicenseState.Revoked: return Language.T("license.number_been_withdrawn_personal_license", "which", which, "number", e.Number, "contact", Contact);
            case LicenseState.Unreadable: return Language.T("license.cant_read_license_file_personal");
            default: return null;
        }
    }

    public static string Detail(LicenseResult e) {
        if (e.Valid) return Language.T("license.number_dated_valid_program_one", "number", e.Number, "date", Data(e));
        return Problem(e, Language.T("license.saved_license")) ?? Language.T("license.fine_personal_noncommercial_use_work");
    }
}
