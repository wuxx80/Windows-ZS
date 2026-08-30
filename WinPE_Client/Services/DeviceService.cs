using System;
using System.Management;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using WinPE_Client.Models;

namespace WinPE_Client.Services
{
    public class DeviceService
    {
        /// <summary>主机名（注册客户端使用）</summary>
        public string GetHostname() => Environment.MachineName;

        /// <summary>首选 MAC 地址（注册客户端使用，取第一个非虚拟网卡）</summary>
        public string GetMacAddress()
        {
            try
            {
                var nic = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                        && n.OperationalStatus == OperationalStatus.Up
                        && n.GetPhysicalAddress().GetAddressBytes().Length > 0)
                    .Select(n => n.GetPhysicalAddress().ToString())
                    .FirstOrDefault();
                if (nic == null)
                {
                    var any = NetworkInterface.GetAllNetworkInterfaces()
                        .FirstOrDefault(n => n.GetPhysicalAddress().GetAddressBytes().Length > 0);
                    nic = any?.GetPhysicalAddress().ToString() ?? "";
                }
                return FormatMac(nic);
            }
            catch { return ""; }
        }

        /// <summary>操作系统版本（注册客户端使用）</summary>
        public string GetOsVersion()
        {
            try { return Environment.OSVersion.VersionString; }
            catch { return "Unknown"; }
        }

        private static string FormatMac(string mac)
        {
            if (string.IsNullOrEmpty(mac)) return "00-00-00-00-00-00";
            var hex = mac.Replace("-", "").Replace(":", "");
            return string.Join("-", Enumerable.Range(0, 6)
                .Select(i => hex.Substring(i * 2, 2)).ToArray());
        }

        public List<DiskInfo> GetDiskInfo()
        {
            var disks = new List<DiskInfo>();
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
                int diskIndex = 0;
                foreach (var disk in searcher.Get())
                {
                    var info = new DiskInfo
                    {
                        Index = diskIndex,
                        Model = disk["Model"]?.ToString() ?? "",
                        Size = Convert.ToInt64(disk["Size"] ?? 0),
                        InterfaceType = disk["InterfaceType"]?.ToString() ?? "",
                        SizeDisplay = FormatSize(Convert.ToInt64(disk["Size"] ?? 0))
                    };
                    info.IsSsd = info.Model.Contains("SSD", StringComparison.OrdinalIgnoreCase)
                        || info.Model.Contains("Solid State", StringComparison.OrdinalIgnoreCase);

                    using var partSearcher = new ManagementObjectSearcher(
                        "ASSOCIATORS OF {Win32_DiskDrive.DeviceID=\"" + disk["DeviceID"] + "\"} WHERE AssocClass=Win32_DiskDriveToDiskPartition");
                    int partIndex = 0;
                    foreach (var part in partSearcher.Get())
                    {
                        var partition = new PartitionInfo
                        {
                            Index = partIndex,
                            Size = Convert.ToInt64(part["Size"] ?? 0),
                            SizeDisplay = FormatSize(Convert.ToInt64(part["Size"] ?? 0)),
                            Type = part["Type"]?.ToString() ?? "",
                        };

                        using var logSearcher = new ManagementObjectSearcher(
                            "ASSOCIATORS OF {Win32_DiskPartition.DeviceID=\"" + part["DeviceID"] + "\"} WHERE AssocClass=Win32_LogicalDiskToPartition");
                        foreach (var log in logSearcher.Get())
                        {
                            partition.DriveLetter = log["DeviceID"]?.ToString() ?? "";
                            partition.Label = log["VolumeName"]?.ToString() ?? "";
                            partition.FileSystem = log["FileSystem"]?.ToString() ?? "";
                            partition.UsedSize = Convert.ToInt64(log["Size"] ?? 0) - Convert.ToInt64(log["FreeSpace"] ?? 0);
                            partition.FreeSize = Convert.ToInt64(log["FreeSpace"] ?? 0);
                        }

                        info.Partitions.Add(partition);
                        partIndex++;
                    }
                    disks.Add(info);
                    diskIndex++;
                }
            }
            catch { }
            return disks;
        }

        public static string FormatSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("F1") + " KB";
            if (bytes < 1024 * 1024 * 1024) return (bytes / (1024.0 * 1024)).ToString("F1") + " MB";
            return (bytes / (1024.0 * 1024 * 1024)).ToString("F1") + " GB";
        }
    }
}