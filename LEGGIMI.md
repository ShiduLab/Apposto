# Apposto 0.1

ShiduLab SofficiWare

App desktop Windows portatile, in italiano, senza installazione. Avvia `Apposto.exe` da una cartella scrivibile. Richiede .NET Framework 4.5 o successivo, normalmente presente in Windows 10/11. Non serve un runtime da scaricare insieme all'app. Il programma non e firmato digitalmente.

## Uso

1. Premi Sfoglia... per aprire Questo PC e scegliere un disco intero o una cartella per Origine e Destinazione. Sono ammesse unita locali, esterne o altri percorsi disponibili, senza vincolo USB. La destinazione deve esistere. Dai un nome alla coppia e premi Salva coppia.
2. Premi Analizza. Ai successivi avvii puoi richiamare la coppia dal menu: avvia anche l'analisi.
3. Controlla l'anteprima. Nuovi e aggiornati sono gia tutti selezionati, anche in sottocartelle diverse. Puoi deselezionarli. Premi APPOSTO! per avviare la copia predisposta.

Conflitti, uguali, file solo destinazione, link esclusi ed errori sono visibili e non copiabili. Il flusso e solo origine > clone. Nessuna cancellazione dei file del clone. Non esiste una funzione di risoluzione forzata dei conflitti in questa versione: vanno valutati separatamente e poi si ripete l'analisi.

## Confronto e protezioni

Il confronto rapido usa percorso relativo, dimensione e timestamp UTC. Quando data e dimensione coincidono presume uguaglianza. Per rilevare anche contenuti diversi con dimensione e data identiche, attiva Verifica approfondita SHA-256. Se i metadati differiscono viene comunque verificato il contenuto: file identici non vengono ricopiati. Tutti i file predisposti sono ancorati a un hash e ricontrollati al momento della copia.

Una destinazione con contenuto diverso e data uguale o piu recente diventa un conflitto. Una destinazione diversa e piu vecchia viene sostituita solo conservando la versione precedente in:

`<clone>\.apposto\storico\<sessione>\<percorso relativo>`

Lo storico e escluso dalla scansione, non viene ripulito automaticamente e puo occupare spazio. Per ripristinare, scegli nello storico la versione desiderata e copiala manualmente nella posizione opportuna, dopo aver verificato i contenuti.

La copia avviene in un file temporaneo nella stessa directory del file finale, con flush, verifica SHA-256 e conservazione del timestamp di ultima modifica. I nuovi file vengono rinominati; le sostituzioni usano File.Replace con backup. Se il filesystem non permette questa operazione la sostituzione fallisce con errore, senza ripiegare su una sovrascrittura priva di protezione. Non viene promessa resistenza ai guasti hardware o alla perdita di alimentazione. Un arresto forzato puo lasciare file nascosti nel nome `.<identificatore>.apposto-tmp`: non sono il file finale e possono essere rimossi a programma chiuso dopo verifica.

Origine e clone devono essere cartelle separate. Symlink, junction e altri reparse point vengono esclusi o rifiutati, inclusi quelli nei percorsi radice. I file non leggibili sono segnalati. L'annullamento interrompe al prossimo punto di controllo, ma una verifica hash gia avviata deve terminare. Le directory vuote non vengono replicate. Sono copiati i dati principali dei file e il timestamp di ultima modifica, non ACL, ADS, attributi speciali o cronologia NTFS. Le cartelle `.apposto` sono riservate ed escluse da entrambi gli alberi.

Evita modifiche contemporanee alle cartelle durante scansione e copia. Sono presenti controlli contro cambiamenti successivi alla scansione, ma questa versione non usa snapshot VSS o blocchi dell'intero albero. Nessuna scansione o copia parte automaticamente all'avvio senza richiamare una coppia. Il click APPOSTO! autorizza la scrittura del piano visibile.

## Portabilita e dischi

Le coppie sono salvate accanto all'eseguibile in `Dati\coppie.xml`, i log in `Dati\Log`. Sposta l'intera cartella dell'app per conservare le impostazioni. Non vengono salvate password. Il volume Windows viene identificato mediante GUID: richiamando una coppia, l'app cerca lo stesso volume sulle lettere disponibili e ricostruisce il percorso relativo. Un volume assente o diverso viene segnalato. Questo recupero riguarda i normali volumi con lettera; percorsi UNC, mount point particolari e volumi ricreati possono richiedere una nuova selezione. Non e stato possibile provare un cambio lettera su una periferica USB fisica in questa sessione.

## Sorgenti e verifiche

`src` contiene il motore, la persistenza e l'interfaccia WinForms. `build.ps1` compila con il compilatore .NET Framework di Windows ed esegue i test senza scaricare dipendenze. `tests\Tests.cs` crea esclusivamente fixture temporanee isolate.

La consegna include un rapporto delle verifiche effettivamente eseguite. Il test dell'interfaccia usa il vero flusso asincrono di scansione e copia su una fixture, senza pilotare mouse e tastiera. Il collaudo non equivale a una certificazione su tutte le versioni Windows e tutti i filesystem esterni.
