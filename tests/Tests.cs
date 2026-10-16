using System;
using System.IO;
using System.Linq;
using Apposto;
class Tests {
 [System.Runtime.InteropServices.DllImport("kernel32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)]static extern uint GetShortPathName(string path,System.Text.StringBuilder result,uint length);
 static int checks;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name); checks++; Console.WriteLine("PASS "+name);}
 static void Put(string root,string p,string text,int seconds){string f=Path.Combine(root,p);Directory.CreateDirectory(Path.GetDirectoryName(f));File.WriteAllText(f,text);File.SetLastWriteTimeUtc(f,new DateTime(2026,1,1).AddSeconds(seconds));}
 static int Main(){string r=Path.Combine(Path.GetTempPath(),"Apposto-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(r);try{
 string s=Path.Combine(r,"origine"),d=Path.Combine(r,"clone");Directory.CreateDirectory(s);Directory.CreateDirectory(d);
 Put(s,"nuovo.txt","nuovo",10);Put(s,"a/b/testo.txt","versione nuova",20);Put(d,"a/b/testo.txt","vecchia",10);
 Put(s,"uguale.txt","uguale",10);Put(d,"uguale.txt","uguale",10);
 Put(s,"metadata.txt","stesso",20);Put(d,"metadata.txt","stesso",10);
 Put(d,"solo-clone.txt","conservami",10);
 Put(s,"recente.txt","origine",10);Put(d,"recente.txt","clone recente",20);
 Put(s,"ambiguo.txt","AAAA",10);Put(d,"ambiguo.txt","BBBB",10);
 var p=Engine.Scan(s,d,true,()=>false,null);
 Check(p.Count(x=>x.State=="Nuovo")==1,"nuovi automatici");
 Check(p.Count(x=>x.State=="Aggiornato")==1,"aggiornati ricorsivi");
 Check(p.Count(x=>x.State=="Uguale")==2,"hash ignora solo metadata");
 Check(p.Count(x=>x.State=="Conflitto")==2,"conflitti e clone recente protetti");
 Check(p.Count(x=>x.State=="Solo destinazione")==1,"solo clone conservato");
 Check(p.Where(x=>x.Copyable).All(x=>x.Selected),"piano preselezionato");
 Engine.Apply(s,d,p,()=>false,null);
 Check(File.ReadAllText(Path.Combine(d,"a/b/testo.txt"))=="versione nuova","gerarchia e contenuto");
 Check(Directory.GetFiles(Path.Combine(d,".apposto"),"*",SearchOption.AllDirectories).Any(f=>File.ReadAllText(f)=="vecchia"),"versione precedente nello storico");
 Check(File.ReadAllText(Path.Combine(d,"recente.txt"))=="clone recente","nessuna sovrascrittura conflitti");
 Check(File.Exists(Path.Combine(d,"solo-clone.txt")),"nessuna cancellazione");
 Check(!Engine.Scan(s,d,true,()=>false,null).Any(x=>x.Copyable),"seconda scansione senza copie");
 bool rejected=false;try{Engine.Scan(s,Path.Combine(s,"interno"),false,()=>false,null);}catch{rejected=true;}Check(rejected,"destinazione dentro origine rifiutata");
 Put(s,"race.txt","prima",10);var race=Engine.Scan(s,d,true,()=>false,null);Put(d,"race.txt","apparso dopo scansione",20);Engine.Apply(s,d,race,()=>false,null);Check(File.ReadAllText(Path.Combine(d,"race.txt"))=="apparso dopo scansione","piano obsoleto non sovrascrive");
 Put(s,"annullato.txt","contenuto",10);var cancel=Engine.Scan(s,d,true,()=>false,null);Engine.Apply(s,d,cancel,()=>true,null);Check(!File.Exists(Path.Combine(d,"annullato.txt")),"annullamento prima della copia");
 string locked=Path.Combine(s,"bloccato.txt");Put(s,"bloccato.txt","locked",10);using(var hold=new FileStream(locked,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){Check(Engine.Scan(s,d,true,()=>false,null).Any(x=>x.Relative=="bloccato.txt"&&x.State=="Non leggibile"),"file non leggibile isolato");}
 Check(!Directory.GetFiles(d,"*.apposto-tmp",SearchOption.AllDirectories).Any(),"nessun parziale visibile");
 Check(File.GetLastWriteTimeUtc(Path.Combine(d,"a/b/testo.txt"))==File.GetLastWriteTimeUtc(Path.Combine(s,"a/b/testo.txt")),"timestamp preservato");
 Put(s,"cambiato.txt","prima",10);var changed=Engine.Scan(s,d,true,()=>false,null);Put(s,"cambiato.txt","dopo",20);Engine.Apply(s,d,changed,()=>false,null);Check(!File.Exists(Path.Combine(d,"cambiato.txt")),"origine cambiata blocca piano");
 Put(s,"interrotto.bin",new string('x',2500000),30);Put(d,"interrotto.bin","precedente",10);var interrupted=Engine.Scan(s,d,true,()=>false,null);foreach(var e in interrupted)e.Selected=e.Relative=="interrotto.bin";bool abort=false;Engine.Apply(s,d,interrupted,()=>abort,t=>abort=true);Check(File.ReadAllText(Path.Combine(d,"interrotto.bin"))=="precedente","annullamento durante copia conserva originale");Check(!Directory.GetFiles(d,"*.apposto-tmp",SearchOption.AllDirectories).Any(),"temporaneo rimosso dopo annullamento");
 Check(Engine.Scan(s,d,false,()=>false,null).First(x=>x.Relative=="ambiguo.txt").State=="Uguale","rapido presume uguaglianza data e dimensione");
 Directory.CreateDirectory(Path.Combine(s,"collisione"));Put(s,"collisione/file.txt","file",10);Put(d,"collisione","occupato",10);Check(Engine.Scan(s,d,true,()=>false,null).Any(x=>x.Relative==Path.Combine("collisione","file.txt")&&x.State=="Conflitto"),"collisione directory contro file");
 bool absent=false;try{Engine.Scan(s,Path.Combine(r,"assente"),true,()=>false,null);}catch(IOException){absent=true;}Check(absent,"disco o cartella assente rifiutato");
 Profiles.Data=Path.Combine(r,"impostazioni");var volume=Profiles.Volume(s);Check(!String.IsNullOrEmpty(volume),"identita volume Windows disponibile");Profiles.Save(new System.Collections.Generic.List<Pair>{new Pair{Name="Testi",Source=s,Destination=d,SourceVolume=volume,DestinationVolume=Profiles.Volume(d)}});var loaded=Profiles.Load();Check(loaded.Count==1&&loaded[0].Source==s,"coppie salvate e ricaricate");Check(Profiles.Resolve(s,volume)==s,"volume corrente riconosciuto");bool wrong=false;try{Profiles.Resolve(s,"volume-inesistente");}catch(IOException){wrong=true;}Check(wrong,"volume diverso non accettato");
 string junction=Path.Combine(s,"link");var psi=new System.Diagnostics.ProcessStartInfo("cmd.exe","/c mklink /J \""+junction+"\" \""+d+"\""){CreateNoWindow=true,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};using(var proc=System.Diagnostics.Process.Start(psi)){proc.WaitForExit();Check(proc.ExitCode==0,"fixture junction creata");}Check(Engine.Scan(s,d,true,()=>false,null).Any(x=>x.Relative=="link"&&x.State=="Escluso"),"junction non seguita");
 string nested=Path.Combine(s,"CloneInterno");Directory.CreateDirectory(nested);var shortName=new System.Text.StringBuilder(32768);uint shortLength=GetShortPathName(nested,shortName,(uint)shortName.Capacity);if(shortLength==0||shortLength>=shortName.Capacity)throw new Exception("Fixture alias 8.3 non verificabile");if(!nested.Equals(shortName.ToString(),StringComparison.OrdinalIgnoreCase)){bool aliasBlocked=false;try{Engine.Scan(s,shortName.ToString(),true,()=>false,null);}catch(IOException){aliasBlocked=true;}Check(aliasBlocked,"radice annidata con alias 8.3 rifiutata");}else Console.WriteLine("SKIP alias 8.3: nomi brevi non disponibili");
 Console.WriteLine("TOTALE "+checks+" verifiche superate. Fixture: "+r);return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
