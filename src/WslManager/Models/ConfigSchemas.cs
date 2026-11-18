namespace WslManager.Models;

/// <summary>Known settings. Anything not listed here is still preserved and editable in the raw view.</summary>
public static class ConfigSchemas
{
    public static List<ConfigEntry> WslConfig() =>
    [
        // [wsl2]
        new("wsl2", "memory", ConfigKind.Size, "Memory assigned to the WSL 2 VM. Default: 50% of Windows memory.", "8GB"),
        new("wsl2", "processors", ConfigKind.Number, "Number of logical processors for the WSL 2 VM. Default: all.", "4"),
        new("wsl2", "swap", ConfigKind.Size, "Swap space added to the VM. 0 disables swap. Default: 25% of Windows memory.", "8GB"),
        new("wsl2", "swapFile", ConfigKind.Path, "Absolute Windows path to the swap VHD. Default: %UserProfile%\\AppData\\Local\\Temp\\swap.vhdx.", @"C:\\temp\\wsl-swap.vhdx"),
        new("wsl2", "networkingMode", ConfigKind.Choice, "Networking mode. 'mirrored' mirrors Windows network interfaces into Linux. Default: NAT.", null, "NAT", "mirrored", "virtioproxy", "none"),
        new("wsl2", "localhostForwarding", ConfigKind.Bool, "Forward ports bound to wildcard/localhost in WSL 2 to the host via localhost. Default: true."),
        new("wsl2", "dnsTunneling", ConfigKind.Bool, "Proxy DNS requests from WSL to Windows via virtualization features. Default: true."),
        new("wsl2", "dnsProxy", ConfigKind.Bool, "(NAT mode) Configure Linux DNS to use a NAT on the host. Default: true."),
        new("wsl2", "firewall", ConfigKind.Bool, "Apply Windows Firewall / Hyper-V firewall rules to WSL traffic. Default: true."),
        new("wsl2", "autoProxy", ConfigKind.Bool, "Make WSL use Windows' HTTP proxy settings. Default: true."),
        new("wsl2", "guiApplications", ConfigKind.Bool, "Enable WSLg (Linux GUI apps). Default: true."),
        new("wsl2", "nestedVirtualization", ConfigKind.Bool, "Allow nested VMs inside WSL 2 (Windows 11+). Default: true."),
        new("wsl2", "pageReporting", ConfigKind.Bool, "Let Windows reclaim unused memory from the VM. Default: true."),
        new("wsl2", "vmIdleTimeout", ConfigKind.Number, "Milliseconds the VM stays idle before shutting down. Default: 60000.", "60000"),
        new("wsl2", "defaultVhdSize", ConfigKind.Size, "Maximum size of new distribution VHDs. Default: 1TB.", "512GB"),
        new("wsl2", "kernel", ConfigKind.Path, "Absolute Windows path to a custom Linux kernel.", @"C:\\kernels\\bzImage"),
        new("wsl2", "kernelModules", ConfigKind.Path, "Absolute Windows path to a custom kernel modules VHD."),
        new("wsl2", "kernelCommandLine", ConfigKind.Text, "Additional kernel command-line arguments.", "vsyscall=emulate"),
        new("wsl2", "safeMode", ConfigKind.Bool, "Run WSL in safe mode, disabling many features (for recovery). Default: false."),
        new("wsl2", "debugConsole", ConfigKind.Bool, "Show a dmesg console window when a WSL 2 distro starts. Default: false."),
        new("wsl2", "maxCrashDumpCount", ConfigKind.Number, "Maximum number of crash dumps to keep.", "10"),
        // [experimental]
        new("experimental", "autoMemoryReclaim", ConfigKind.Choice, "Automatically release cached memory after CPU usage drops.", null, "disabled", "gradual", "dropCache"),
        new("experimental", "sparseVhd", ConfigKind.Bool, "Create newly installed distributions with a sparse VHD (space is reclaimed automatically)."),
        new("experimental", "hostAddressLoopback", ConfigKind.Bool, "(Mirrored mode) Allow host and container to reach each other via an IP assigned to the host."),
        new("experimental", "bestEffortDnsParsing", ConfigKind.Bool, "(DNS tunneling) Strip unknown records from DNS responses instead of failing."),
        new("experimental", "dnsTunnelingIpAddress", ConfigKind.Text, "(DNS tunneling) Nameserver IP written to resolv.conf. Default: 10.255.255.254.", "10.255.255.254"),
        new("experimental", "initialAutoProxyTimeout", ConfigKind.Number, "(autoProxy) Milliseconds to wait for proxy info when WSL starts. Default: 1000.", "1000"),
        new("experimental", "ignoredPorts", ConfigKind.Text, "(Mirrored mode) Comma-separated ports Linux may bind even if Windows uses them.", "3000,9000"),
        // [general]
        new("general", "instanceIdleTimeout", ConfigKind.Number, "Milliseconds a distro stays running after its last process exits.", "15000"),
    ];

    public static List<ConfigEntry> WslConf() =>
    [
        new("boot", "systemd", ConfigKind.Bool, "Run systemd as PID 1."),
        new("boot", "command", ConfigKind.Text, "Command run as root when the distro starts.", "service docker start"),
        new("boot", "protectBinfmt", ConfigKind.Bool, "Prevent systemd from overriding WSL's binfmt (Windows interop) registration."),
        new("user", "default", ConfigKind.Text, "Default user when launching the distro.", "alice"),
        new("automount", "enabled", ConfigKind.Bool, "Auto-mount Windows drives under the automount root. Default: true."),
        new("automount", "root", ConfigKind.Text, "Directory where Windows drives are mounted. Default: /mnt/.", "/mnt/"),
        new("automount", "options", ConfigKind.Text, "DrvFs mount options for Windows drives.", "metadata,umask=22,fmask=11"),
        new("automount", "mountFsTab", ConfigKind.Bool, "Process /etc/fstab at startup. Default: true."),
        new("network", "hostname", ConfigKind.Text, "Hostname of the distro. Default: Windows hostname."),
        new("network", "generateHosts", ConfigKind.Bool, "Generate /etc/hosts automatically. Default: true."),
        new("network", "generateResolvConf", ConfigKind.Bool, "Generate /etc/resolv.conf automatically. Default: true."),
        new("interop", "enabled", ConfigKind.Bool, "Allow launching Windows executables from Linux. Default: true."),
        new("interop", "appendWindowsPath", ConfigKind.Bool, "Append the Windows PATH to $PATH. Default: true."),
        new("gpu", "enabled", ConfigKind.Bool, "Give Linux apps access to the Windows GPU. Default: true."),
        new("time", "useWindowsTimezone", ConfigKind.Bool, "Sync the Linux timezone with Windows. Default: true."),
    ];
}
