# HusTracker

**Koll på huset — vad det har kostat, vad som behöver göras härnäst, och var papperen finns.**

---

## Varför

De flesta husägare vet ungefär vad huset är *värt*. Nästan ingen vet vad det har *kostat*.

Inte köpeskillingen — den är lätt. Utan allt efteråt: taket, värmepumpen, fasadmålningen,
dräneringen, de tre gånger någon var här och lagade något. Var och en är en enskild händelse, betald
från olika konton, ihågkommen i ett halvår. Efter tio år finns ingen som har helhetsbilden.

Det leder till tre saker som är lätta att känna igen:

**Pengarna försvinner i det tysta.** Du minns att taket kostade "runt hundra tusen", men inte om det
var 2019 eller 2020, och inte vad allt det andra summerade till. Frågar någon vad ni lagt på huset
blir svaret en gissning.

**Underhåll blir akut i stället för planerat.** Man kommer ihåg att måla om när färgen flagnar, och
att pannan skulle servas när den låter konstigt. Det som borde vara en förutsägbar årlig kostnad
blir en rad obehagliga överraskningar.

**Papperen finns "någonstans".** Kvittot på fönsterbytet, garantibeviset på värmepumpen,
besiktningsprotokollet från köpet. De behövs sällan — men när de behövs, behövs de på riktigt.

Och så den obekväma frågan som ingen mäklarvärdering svarar på:

> Värdet har stigit 600 000 kr sedan vi köpte. Men vi har lagt in 800 000 kr.
> Hur går det egentligen?

HusTracker är byggt för att svara på just den frågan — och för att du aldrig ska behöva gissa igen.

---

## Vad appen innehåller

### Översikt

Startsidan svarar på "hur ligger vi till?" utan att du behöver leta:

- **Nuvarande värde** mot köpeskilling, med förändringen sedan köpet.
- **Mot insatt kapital** — värdet minus det du faktiskt satt in: köpeskilling plus renoveringar och
  nyinvesteringar. Det är den ärliga siffran, och ofta en annan än den man hoppades på.
- **Utgifter** uppdelade på Underhåll, Renovering, Nyinvestering och Inköp, och på *I år*,
  *Rullande 12 månader*, *Förra året*, *Totalt* och *Snitt per år*. Rullande 12 månader finns för att
  "i år" i januari annars ser ut som ett ras.
- **Underhåll som andel av värdet** jämfört med den vanliga tumregeln på cirka 1 % per år — inte ett
  mål, utan ett sätt att se var ni ligger.
- **Årets budget** mot utfall, per typ av arbete.
- **Vart pengarna går** — de dyraste delarna av huset, med andelar.
- **Pågående och planerade projekt**, med beräknad kostnad kvar att betala.
- **Underhåll att se över** — det som är försenat eller snart förfaller.
- En **tidslinje** år för år, som fälls ut till kvartal, där varje värdering och projekt går att klicka
  sig vidare från. Härifrån kan du också snabbregistrera något du just kommit på.

### Projekt

Allt arbete på huset, oavsett storlek. Varje projekt har:

- **Typ av arbete** — Underhåll (bevara eller ersätta befintligt), Renovering (förbättra befintligt),
  Nyinvestering (tillföra något nytt) eller Inköp (lös egendom och utrustning). Skillnaden avgör var
  pengarna hamnar i siffrorna, så appen förklarar den där du väljer.
- **Komponent** — vilken del av huset det gäller: tak, fasad, VVS, och så vidare.
- **Status och prioritet**, plus en flagga för brådskande.
- **Datum** för planerad start, faktisk start och slutförande.
- **Kostnadsposter** med eget datum och typ (material, arbetskraft, verktyg, tillstånd, övrigt) —
  så ett jobb över årsskiftet hamnar på rätt år, precis som pengarna gjorde.
- **Entreprenör** med kontaktuppgifter, offert och offertdatum.
- **Milstolpar** för längre projekt.
- **Dokument** kopplade direkt till projektet.
- Frivilliga fält för förväntad värdeökning, livslängd och energibesparing.

### Underhållsplan

Räknas fram automatiskt — det finns inget att hålla uppdaterat för hand.

Varje komponent har ett rekommenderat intervall. Appen tittar på det senaste slutförda underhålls-
eller renoveringsprojektet för den delen och räknar ut när det är dags igen: **Försenat**, **Snart**
eller **Ok**. Finns inget loggat används husets byggår som utgångspunkt, tydligt märkt som ett
antagande och inte som utfört arbete.

Det betyder att planen blir mer korrekt ju mer du använder appen, i stället för att bli inaktuell.

### Värderingar

Registrera värderingar över tid — mäklarutlåtanden, egna bedömningar, taxeringsvärden — med datum,
källa och anteckning. Den senaste driver värdesiffrorna på översikten, och alla syns i tidslinjen.

### Budget

Sätt en budget per år och typ av arbete. Utfallet summeras automatiskt från projektens kostnadsposter,
så det kan aldrig hamna i otakt med verkligheten. År utan budget men med utgifter syns också — pengar
som spenderats utan plan göms inte undan.

### Dokument

Kvitton, garantier, lagfart, besiktningsprotokoll, fakturor och foton. Varje dokument får en titel,
en kategori och ett datum, kan kopplas till ett projekt, och går att söka och sortera fram.

Filerna kan ligga i appens egen lagring **eller i din egen Google Drive** — då skapar appen en
mappstruktur åt dig, med *Allmänt* för allt löst och en mapp per projekt under *Projekt*.

### Komponenter

Vilka delar huset består av, och hur ofta de brukar behöva ses över. Det finns ett gemensamt
utgångsläge, men **varje bostad har sin egen lista** — ett radhus har inget staket att måla, och just
ert tak kanske håller längre än schablonen. Ändra fritt, och hämta tillbaka från det gemensamma
registret när du vill.

### Flera bostäder, och dela med någon

Lägg upp fler än en bostad — villan, fritidshuset, föräldrarnas hus. Varje bostad är privat för sina
medlemmar, och du bjuder in den du vill dela med. Alla som delar ser samma bild och kan fylla på.

Det finns också en **demobostad** att titta runt i innan man lägger upp sin egen.

### Praktiskt

Logga in med Google eller e-post och lösenord. Fungerar lika bra i mobilen som på datorn — tabeller
scrollar i sidled i stället för att klämmas ihop. Allt på svenska.

---

## Vad du får ut av det

**Ett ärligt svar på vad huset kostat.** Inte en känsla, utan en summa — uppdelad på underhåll,
renovering, nyinvestering och inköp, och satt i relation till vad huset är värt. Inklusive den
obekväma varianten: hur står vi oss mot det vi faktiskt satt in?

**Underhåll som en planerad kostnad.** När du ser att fasaden är försenad och pannan förfaller om
åtta månader kan du budgetera för det, hämta in offert i lugn och ro och göra det på sommaren i
stället för i panik i november.

**Papper som går att hitta.** Garantibeviset när maskinen går sönder. Kvittot när försäkringsbolaget
frågar. Besiktningsprotokollet när du ska sälja. Tio sekunder i stället för en eftermiddag i en
pärm — eller ett samtal till hantverkaren som gjorde jobbet 2021.

**Ett underlag när ni ska sälja.** En dokumenterad historik över vad som gjorts, när, av vem och till
vilken kostnad är något konkret att visa en spekulant — i stället för "taket är väl omlagt för några
år sedan, tror jag".

**Samma bild för alla som bor där.** Ingen som ensam bär historiken i huvudet, och ingen som behöver
fråga vad något kostade.

**Ett hus som blir mer begripligt ju längre du äger det.** Varje loggat projekt gör underhållsplanen
mer träffsäker och snittsiffrorna mer meningsfulla. Efter några år har du något ingen mäklarvärdering
kan ge dig: hela bilden av vad det faktiskt innebär att äga just det här huset.
