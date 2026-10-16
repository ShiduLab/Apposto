using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Serialization;
namespace Apposto {
 public class Pair {
  public string Name,Source,Destination,SourceVolume,DestinationVolume;
  public override string ToString(){return Name;}
 }
 public static class Profiles {
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool GetVolumeNameForVolumeMountPoint(string mount,StringBuilder name,int count);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool GetVolumePathName(string file,StringBuilder path,int count);
  public static string Data=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dati");
  public static string Volume(string p){var mount=new StringBuilder(1024);var id=new StringBuilder(1024);return GetVolumePathName(Path.GetFullPath(p),mount,mount.Capacity)&&GetVolumeNameForVolumeMountPoint(mount.ToString(),id,id.Capacity)?id.ToString():"";}
  public static string Resolve(string path,string volume){
   if(String.IsNullOrEmpty(volume))return path;
   if(String.Equals(Volume(path),volume,StringComparison.OrdinalIgnoreCase))return path;
   foreach(var drive in DriveInfo.GetDrives())try{if(drive.IsReady&&Volume(drive.RootDirectory.FullName)==volume)return Path.Combine(drive.RootDirectory.FullName,path.Substring(Path.GetPathRoot(path).Length));}catch{}
   throw new IOException("Disco della coppia assente o identita diversa: "+path);
  }
  public static List<Pair> Load(){string p=Path.Combine(Data,"coppie.xml");if(!File.Exists(p))return new List<Pair>();using(var f=File.OpenRead(p))return (List<Pair>)new XmlSerializer(typeof(List<Pair>)).Deserialize(f);}
  public static void Save(List<Pair> pairs){Directory.CreateDirectory(Data);string p=Path.Combine(Data,"coppie.xml"),tmp=p+".tmp";using(var f=File.Create(tmp))new XmlSerializer(typeof(List<Pair>)).Serialize(f,pairs);if(File.Exists(p))File.Replace(tmp,p,p+".bak");else File.Move(tmp,p);}
 }
}
