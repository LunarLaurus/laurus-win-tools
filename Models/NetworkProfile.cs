namespace NetProfileSwitcher.Models;

public class NetworkProfile
{
    public string Name { get; set; } = "";
    public bool UseDhcp { get; set; } = true;
    public string Ip { get; set; } = "";
    public string Subnet { get; set; } = "255.255.255.0";
    public string Gateway { get; set; } = "";
    public string Dns1 { get; set; } = "";
    public string Dns2 { get; set; } = "";
    public List<string> LinkedSsids { get; set; } = new();
}
