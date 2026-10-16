using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace Apposto {
 public class Entry {
  public string Relative,State,Detail,SourceHash,DestinationHash;
  public long SourceSize,DestinationSize;
  public DateTime SourceTime,DestinationTime;
  public bool Selected;
  public bool Copyable {get{return State=="Nuovo"||State=="Aggiornato";}}
 }
 public static class Engine {
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern uint GetFinalPathNameByHandle(SafeFileHandle handle,StringBuilder path,uint length,uint flags);
  static string PhysicalRoot(string path){
   using(var handle=CreateFile(path.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,0,7,IntPtr.Zero,3,0x02000000,IntPtr.Zero)){
    if(handle.IsInvalid)throw new IOException("Impossibile verificare la cartella fisica: "+path,new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()));
    var result=new StringBuilder(32768);uint length=GetFinalPathNameByHandle(handle,result,(uint)result.Capacity,2);
    if(length==0||length>=result.Capacity)throw new IOException("Impossibile verificare l'identita fisica della cartella: "+path);
    return result.ToString().TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
   }
  }
  public static string Root(string p){return Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;}
  static bool Inside(string a,string b){return a.StartsWith(b,StringComparison.OrdinalIgnoreCase);}
  public static void SafePath(string p){
   string q=Path.GetFullPath(p);
   while(!String.IsNullOrEmpty(q)) {if((File.Exists(q)||Directory.Exists(q))&&(File.GetAttributes(q)&FileAttributes.ReparsePoint)!=0)throw new IOException("Link o junction escluso: "+q);q=Path.GetDirectoryName(q);}
  }
  public static void Validate(string s,string d){
   s=Root(s);d=Root(d);if(Inside(s,d)||Inside(d,s))throw new IOException("Origine e clone devono essere cartelle separate, non contenute l'una nell'altra.");
   SafePath(s);SafePath(d);if(!Directory.Exists(s)||!Directory.Exists(d))throw new IOException("Cartella o disco assente. Ricollega il disco e verifica i percorsi.");
   string physicalS=PhysicalRoot(s),physicalD=PhysicalRoot(d);if(Inside(physicalS,physicalD)||Inside(physicalD,physicalS))throw new IOException("Origine e clone coincidono o sono annidati sullo stesso volume, anche tramite alias.");
  }
  static string HashStream(Stream f){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(f)).Replace("-","");}
  static string Hash(string f){using(var s=new FileStream(f,FileMode.Open,FileAccess.Read,FileShare.Read))return HashStream(s);}
  static Dictionary<string,string> Walk(string root,List<Entry> errors,Func<bool> cancel){
   var found=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);var stack=new Stack<string>();stack.Push(root);
   while(stack.Count>0){if(cancel())throw new OperationCanceledException();string dir=stack.Pop();try{
    SafePath(dir);
    foreach(string p in Directory.GetFileSystemEntries(dir)){
     string rel=p.Substring(root.Length);if(rel.Split(Path.DirectorySeparatorChar).Any(x=>x.Equals(".apposto",StringComparison.OrdinalIgnoreCase)))continue;
     try {var attr=File.GetAttributes(p);if((attr&FileAttributes.ReparsePoint)!=0){errors.Add(new Entry{Relative=rel,State="Escluso",Detail="Link o junction: non seguito"});continue;}
      if((attr&FileAttributes.Directory)!=0)stack.Push(p);else {using(var f=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.Read)){}found.Add(rel,p);}
     }catch(Exception e){errors.Add(new Entry{Relative=rel,State="Non leggibile",Detail=e.Message});}
    }
   }catch(Exception e){if(e is OperationCanceledException)throw;errors.Add(new Entry{Relative=dir.Substring(root.Length),State="Non leggibile",Detail=e.Message});}}
   return found;
  }
  public static List<Entry> Scan(string source,string destination,bool deep,Func<bool> cancel,Action<string> progress){
   Validate(source,destination);string s=Root(source),d=Root(destination);var rows=new List<Entry>();
   var a=Walk(s,rows,cancel);var b=Walk(d,rows,cancel);
   foreach(var pair in a){if(cancel())throw new OperationCanceledException();var e=new Entry{Relative=pair.Key};rows.Add(e);try{
    if(progress!=null)progress(pair.Key);string target=Path.Combine(d,pair.Key);SafePath(target);var si=new FileInfo(pair.Value);e.SourceSize=si.Length;e.SourceTime=si.LastWriteTimeUtc;
    // A hash also anchors each proposed copy to exactly the bytes reviewed.
    if(!b.ContainsKey(pair.Key)){
     if(Directory.Exists(target)||BlockedParent(target,d)||rows.Any(x=>x!=e&&x.Relative.Equals(pair.Key,StringComparison.OrdinalIgnoreCase)&&x.State=="Non leggibile")){e.State="Conflitto";e.Detail="Destinazione occupata o non accessibile";}
     else{e.SourceHash=Hash(pair.Value);e.State="Nuovo";e.Detail="Da aggiungere";}
    }else{
     var di=new FileInfo(target);e.DestinationSize=di.Length;e.DestinationTime=di.LastWriteTimeUtc;
     if(!deep&&e.SourceSize==e.DestinationSize&&e.SourceTime==e.DestinationTime){e.State="Uguale";e.Detail="Dimensione e data uguali (confronto rapido)";}
     else{
      e.SourceHash=Hash(pair.Value);e.DestinationHash=Hash(target);
      if(e.SourceHash==e.DestinationHash){e.State="Uguale";e.Detail="Contenuto SHA-256 identico";}
      else if(e.DestinationTime>=e.SourceTime){e.State="Conflitto";e.Detail="Contenuto diverso, clone piu recente o data uguale. Nessuna copia.";}
      else{e.State="Aggiornato";e.Detail="Sostituzione con precedente versione nello storico";}
     }
    }
    e.Selected=e.Copyable;
   }catch(Exception ex){e.State="Non leggibile";e.Detail=ex.Message;e.Selected=false;}}
   foreach(var pair in b)if(!a.ContainsKey(pair.Key))rows.Add(new Entry{Relative=pair.Key,State="Solo destinazione",Detail="Conservato senza modifiche"});
   return rows.OrderBy(x=>x.Relative,StringComparer.OrdinalIgnoreCase).ToList();
  }
  static bool BlockedParent(string p,string root){p=Path.GetDirectoryName(p);while(p!=null&&Inside(Root(p),root)){if(File.Exists(p))return true;p=Path.GetDirectoryName(p);}return false;}
  public static void Apply(string source,string destination,IEnumerable<Entry> plan,Func<bool> cancel,Action<string> progress){
   Validate(source,destination);string s=Root(source),d=Root(destination);string run=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N");
   foreach(var e in plan.Where(x=>x.Copyable&&x.Selected)){
    if(cancel())break;string tmp=null;
    try{
     string src=Path.GetFullPath(Path.Combine(s,e.Relative)),dst=Path.GetFullPath(Path.Combine(d,e.Relative));
     if(!Inside(src,s)||!Inside(dst,d))throw new IOException("Percorso fuori dalle cartelle autorizzate");
     SafePath(src);SafePath(dst);
     using(var input=new FileStream(src,FileMode.Open,FileAccess.Read,FileShare.Read)){
      if(HashStream(input)!=e.SourceHash)throw new IOException("Origine cambiata dopo scansione: ripetere analisi");input.Position=0;
      if(e.State=="Nuovo"&&(File.Exists(dst)||Directory.Exists(dst)))throw new IOException("Destinazione apparsa dopo scansione");
      if(e.State=="Aggiornato"&&(!File.Exists(dst)||Hash(dst)!=e.DestinationHash||File.GetLastWriteTimeUtc(dst)!=e.DestinationTime))throw new IOException("Clone cambiato dopo scansione: protetto");
      Directory.CreateDirectory(Path.GetDirectoryName(dst));SafePath(dst);
      tmp=Path.Combine(Path.GetDirectoryName(dst),"."+Guid.NewGuid().ToString("N")+".apposto-tmp");
      using(var output=new FileStream(tmp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
       byte[] buffer=new byte[1024*1024];int n;while((n=input.Read(buffer,0,buffer.Length))>0){if(cancel())throw new OperationCanceledException();output.Write(buffer,0,n);if(progress!=null)progress("Copia "+e.Relative+" | "+output.Position+" / "+input.Length+" byte");}output.Flush(true);
      }
      if(Hash(tmp)!=e.SourceHash)throw new IOException("Verifica della copia fallita");
      File.SetLastWriteTimeUtc(tmp,e.SourceTime);if(cancel())throw new OperationCanceledException();SafePath(dst);
      if(e.State=="Nuovo")File.Move(tmp,dst);
      else{
       // Last validation immediately before commit; File.Replace fails safely on unsupported filesystems.
       if(Hash(dst)!=e.DestinationHash||File.GetLastWriteTimeUtc(dst)!=e.DestinationTime)throw new IOException("Clone cambiato durante copia: protetto");
       string backup=Path.Combine(d,".apposto","storico",run,e.Relative);SafePath(backup);Directory.CreateDirectory(Path.GetDirectoryName(backup));
       File.Replace(tmp,dst,backup,true);
      }
      tmp=null;e.State="Copiato";e.Selected=false;e.Detail="Copia verificata SHA-256";
     }
     if(progress!=null)progress("OK "+e.Relative);
    }catch(OperationCanceledException){e.Detail="Annullato: originale conservato";break;}
    catch(Exception ex){e.State="Errore copia";e.Detail=ex.Message;e.Selected=false;if(progress!=null)progress("ERRORE "+e.Relative+": "+ex.Message);}
    finally{if(tmp!=null)try{File.Delete(tmp);}catch{}}
   }
  }
 }
}
