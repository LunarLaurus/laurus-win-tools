namespace NetProfileSwitcher.Models;

public record AdapterInfo(
    bool IsDhcp,
    string Ip,
    string Subnet,
    string Gateway,
    string Dns1,
    string Dns2
);
