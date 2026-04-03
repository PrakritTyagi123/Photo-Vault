namespace PhotoVault.Core.Models;

public class EditSettings
{
    public double Brightness { get; set; }
    public double Contrast { get; set; }
    public double Saturation { get; set; }
    public double Sharpen { get; set; }
    public double Vignette { get; set; }
    public string Filter { get; set; } = "None";
    public bool HasEdits => Brightness != 0 || Contrast != 0 || Saturation != 0 || Sharpen != 0 || Vignette != 0 || Filter != "None";
}
