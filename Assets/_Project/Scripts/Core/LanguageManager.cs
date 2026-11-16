using System.Collections.Generic;
using UnityEngine;

namespace Astra
{
    /// <summary>Keyed, data-driven UI and dialogue strings for the three supported languages.</summary>
    public sealed class LanguageManager : MonoBehaviour
    {
        public static LanguageManager Instance { get; private set; }
        private static readonly Dictionary<string, string[]> Lines = new Dictionary<string, string[]>
        {
            { "welcome", new[] { "Hello, astronaut! Welcome to ASTRA! I'm ASTRO, and I'll be your space companion. Before we explore the Moon, let me show you around!", "Sawubona, somkhathi! Siyakwamukela ku-ASTRA! Ngingu-ASTRO, umngane wakho wasemkhathini. Ngaphambi kokuhlola iNyanga, ake ngikubonise indawo!", "Hallo, ruimtevaarder! Welkom by ASTRA! Ek is ASTRO, jou ruimtevriend. Voor ons die Maan verken, wys ek jou graag rond!" } },
            { "ready", new[] { "Great work, astronaut! You're ready for your first mission. Let's head to the Moon!", "Wenze kahle, somkhathi! Usulungele umsebenzi wakho wokuqala. Asiye eNyangeni!", "Mooi so, ruimtevaarder! Jy is gereed vir jou eerste sending. Kom ons gaan Maan toe!" } },
            { "welcome.title", new[] { "HELLO, ASTRONAUT!", "SAWUBONA, SOMKHATHI!", "HALLO, RUIMTEVAARDER!" } },
            { "welcome.body", new[] { "ASTRO is your space buddy. Learn one step at a time. Replay or skip practice whenever you like.", "U-ASTRO ungumngane wakho wasemkhathini. Funda isinyathelo esisodwa ngesikhathi. Phinda noma weqe ukuzilolonga noma nini.", "ASTRO is jou ruimtevriend. Leer een stap op 'n slag. Herhaal of slaan oefening enige tyd oor." } },
            { "welcome.setup", new[] { "MEET ASTRO · PRACTISE · CHOOSE A MISSION", "HLANGANA NO-ASTRO · ZIJWAYEZE · KHETHA UMSEBENZI", "ONTMOET ASTRO · OEFEN · KIES 'N SENDING" } },
            { "welcome.returning", new[] { "WELCOME BACK · PRACTISE OR CHOOSE A MISSION", "WAMUKELWE FUTHI · ZIJWAYEZE NOMA UKHETHE UMSEBENZI", "WELKOM TERUG · OEFEN OF KIES 'N SENDING" } },
            { "welcome.start", new[] { "LET'S GET READY", "ASIQALE", "KOM ONS BEGIN" } },
            { "welcome.skip", new[] { "SKIP TUTORIAL", "NGIYAKWAZI UKUDLALA — YEQA", "EK WEET HOE OM TE SPEEL — SKIP" } },
            { "welcome.settings", new[] { "CHANGE MY SETTINGS", "SHINTSHA IZILUNGISELELO", "VERANDER INSTELLINGS" } },
            { "settings.title", new[] { "MAKE ASTRA FIT YOU", "YENZA I-ASTRA IKULUNGELE", "STEL ASTRA VIR JOU IN" } },
            { "settings.language", new[] { "LANGUAGE", "ULIMI", "TAAL" } },
            { "settings.reading", new[] { "HOW SHOULD ASTRO HELP?", "U-ASTRO AKUSIZE KANJANI?", "HOE MOET ASTRO HELP?" } },
            { "settings.assistance", new[] { "HOW MUCH HELP?", "LUDINGA USIZO OLUNGAKANANI?", "HOEVEEL HULP?" } },
            { "settings.noreading", new[] { "NO-READING MODE", "IMODI YOKUNGAFUNDI", "LEES-VRYE MODUS" } },
            { "settings.text", new[] { "TEXT SIZE", "USAYIZI WOMBHALO", "TEKSGROOTTE" } },
            { "settings.subtitles", new[] { "SUBTITLES", "IMIBHALO YENKULUMO", "ONDERSKRIFTE" } },
            { "settings.contrast", new[] { "HIGH CONTRAST", "UMEHLUKO OMKHULU", "HOË KONTRAS" } },
            { "settings.voice", new[] { "VOICE VOLUME", "IVOLUMU YEZWI", "STEMVOLUME" } },
            { "settings.mouse", new[] { "CLICK-TO-WALK", "CHOFOZA UKUHAMBA", "KLIK-OM-TE-LOOP" } },
            { "settings.controls", new[] { "CHANGE KEYS", "SHINTSHA IZIKHIYE", "VERANDER SLEUTELS" } },
            { "settings.done", new[] { "SAVE AND CONTINUE", "GCINA UQHUBEKE", "STOOR EN GAAN VOORT" } },
            { "settings.back", new[] { "BACK", "BUYELA", "TERUG" } },
            { "mode.voice", new[] { "VOICE", "IZWI", "STEM" } },
            { "mode.visual", new[] { "PICTURES", "IZITHOMBE", "PRENTE" } },
            { "mode.combined", new[] { "BOTH", "KOKUBILI", "ALBEI" } },
            { "assist.more", new[] { "MORE GUIDANCE", "USIZO OLWENGEZIWE", "MEER LEIDING" } },
            { "assist.balanced", new[] { "SOME HINTS", "AMACEBO AMANINGI", "SOMMIGE WENKE" } },
            { "assist.independent", new[] { "EXPLORE", "ZIHLOLELE", "VERKEN" } },
            { "tutorial.step", new[] { "ACTIVITY", "UMSEBENZI", "AKTIWITEIT" } },
            { "tutorial.meet.title", new[] { "MEET YOUR ASTRONAUT", "Hlangana NOSOMKHATHI WAKHO", "ONTMOET JOU RUIMTEVAARDER" } },
            { "tutorial.meet.body", new[] { "This is your astronaut. ASTRO is right here beside you. Take your time; you can try anything again.", "Lona ngusomkhathi wakho. U-ASTRO useduze kwakho. Thatha isikhathi sakho; ungaphinda uzame noma yini.", "Dit is jou ruimtevaarder. ASTRO is hier by jou. Neem jou tyd; jy kan enigiets weer probeer." } },
            { "tutorial.move.title", new[] { "LEARN TO MOVE", "FUNDA UKUHAMBA", "LEER BEWEEG" } },
            { "tutorial.move.body", new[] { "Use WASD/arrow keys, a gamepad's left stick, or a joystick's main stick to walk. Click the ground to walk there.", "Sebenzisa u-WASD/izinkinobho zemicibisholo, induku yesokunxele yesilawuli segeyimu, noma induku eyinhloko ye-joystick ukuze uhambe. Chofoza phansi ukuze uye lapho.", "Gebruik WASD/pyltoetse, die gamepad se linker-stok of die joystick se hoofstok om te loop. Klik op die grond om daarheen te loop." } },
            { "tutorial.look.title", new[] { "LOOK AROUND", "BHEKA NXAZONKE", "KYK ROND" } },
            { "tutorial.look.body", new[] { "Use right-drag with the mouse, the gamepad's right stick, or a joystick's twist control to look around.", "Sebenzisa igundane langakwesokudla, induku yesokudla yesilawuli segeyimu, noma ukujika kwe-joystick ukuze ubheke nxazonke.", "Gebruik die muis se regtermuisknoppie, die gamepad se regter-stok of die joystick se draaibeheer om rond te kyk." } },
            { "tutorial.interact.title", new[] { "TRY AN INTERACTION", "ZAMA UKUSEBENZISA", "PROBEER 'N INTERAKSIE" } },
            { "tutorial.interact.body", new[] { "Walk to the glowing practice panel. When the arrow points down, press E, gamepad X, or the joystick trigger.", "Hamba uye kuphaneli ekhanyayo yokuzilolonga. Uma umcibisholo ukhomba phansi, cindezela u-E, u-X wesilawuli segeyimu, noma i-trigger ye-joystick.", "Loop na die gloeiende oefenpaneel. Wanneer die pyl af wys, druk E, gamepad X of die joystick se sneller." } },
            { "tutorial.equipment.title", new[] { "MEET THE EQUIPMENT", "HLOLA IZINSIZA", "ONTMOET DIE TOERUSTING" } },
            { "tutorial.equipment.body", new[] { "Tap an equipment picture. ASTRO will tell you what it does.", "Thinta isithombe sensiza. U-ASTRO uzokutshela ukuthi yenzani.", "Tik op 'n toerustingprent. ASTRO sal jou vertel waarvoor dit is." } },
            { "tutorial.task.title", new[] { "POWER UP A PRACTICE PANEL", "VULA IPHANELI YOKUZIJWAYEZA", "SKAKEL 'N OEFENPANEEL AAN" } },
            { "tutorial.task.body", new[] { "Follow the arrow to the small practice solar panel and press E, gamepad X, or the joystick trigger. Watch it light up!", "Landela umcibisholo uye kuphaneli yelanga lokuzijwayeza, bese ucindezela u-E, u-X wesilawuli segeyimu, noma i-trigger ye-joystick. Yibuke ikhanya!", "Volg die pyl na die klein oefensonpaneel en druk E, gamepad X of die joystick se sneller. Kyk hoe dit lig gee!" } },
            { "tutorial.next", new[] { "I DID IT — NEXT", "NGIKWENZILE — QHUBEKA", "EK HET DIT GEDOEN — VOLGENDE" } },
            { "tutorial.hint.button", new[] { "SHOW ME WHAT TO DO", "NGIKHOMBISE OKUFANELE NGIKWENZE", "WYS MY WAT OM TE DOEN" } },
            { "tutorial.repeat", new[] { "SAY THAT AGAIN", "YIPHINDE", "SÊ DIT WEER" } },
            { "tutorial.skip", new[] { "SKIP TUTORIAL", "YEQA ISIFUNDO", "SKIP TUTORIAAL" } },
            { "tutorial.complete", new[] { "GREAT WORK!", "WENZE KAHLE!", "MOOI SO!" } },
            { "mission.title", new[] { "MISSION SELECT", "Khetha UMSEBENZI", "SENDINGKEUSE" } },
            { "mission.first", new[] { "FIRST LIGHT", "UKUKHANYA KOKUQALA", "EERSTE LIG" } },
            { "mission.launch", new[] { "LAUNCH MISSION", "QALA UMSHINI", "BEGIN SENDING" } },
            { "mission.replay", new[] { "PRACTISE WITH ASTRO", "ZILOLONGE NO-ASTRO", "OEFEN MET ASTRO" } },
            { "mission.access", new[] { "ACCESSIBILITY SETTINGS", "IZILUNGISELELO ZOKUFINYELELA", "TOEGANKLIKHEIDSINSTELLINGS" } },
            { "objective.0", new[] { "Activate communications", "Vula ezokuxhumana", "Skakel kommunikasie aan" } },
            { "objective.1", new[] { "Bring solar power online", "Vula amandla elanga", "Skakel sonkrag aan" } },
            { "objective.2", new[] { "Inspect life support", "Hlola ukusekelwa kokuphila", "Inspekteer lewensondersteuning" } },
            { "objective.3", new[] { "Prepare radiation shelter", "Lungisa indawo yokukhosela emisebeni", "Berei stralingskuiling voor" } },
            { "objective.4", new[] { "Grow and harvest food", "Tshala bese uvuna ukudla", "Kweek en oes kos" } },
            { "objective.5", new[] { "Respond to solar-particle drill", "Phendula esivivinyweni sezinhlayiya zelanga", "Reageer op sondeeltjie-oefening" } },
            { "objective.6", new[] { "Collect a virtual sample", "Qoqa isampula elingokoqobo", "Versamel 'n virtuele monster" } },
            { "hint.move", new[] { "Try a gentle step. There is no rush.", "Zama ukuhamba kancane. Akukho ukushesha.", "Probeer 'n sagte tree. Daar is geen haas nie." } },
            { "hint.look", new[] { "Try holding the right mouse button while moving the mouse.", "Zama ukubamba inkinobho engakwesokudla uhambise igundane.", "Hou die regtermuisknoppie in terwyl jy die muis beweeg." } },
            { "equipment.oxygen", new[] { "OXYGEN · This system adds breathable air in our game.", "UMOYA · Lolu hlelo lunezela umoya emdlalweni wethu.", "SUURSTOF · Hierdie stelsel voeg asemhalingslug in ons speletjie by." } },
            { "equipment.water", new[] { "WATER · The crew needs water to drink.", "AMANZI · Abasebenzi badinga amanzi okuphuza.", "WATER · Die bemanning het water nodig om te drink." } },
            { "equipment.solar", new[] { "SUN · Solar panels make game power from sunlight.", "ILANGA · Amaphaneli enza amandla okudlala ngokukhanya kwelanga.", "SON · Sonpanele maak speletjiekrag uit sonlig." } },
            { "equipment.food", new[] { "FOOD · The greenhouse grows food for the crew.", "UKUDLA · Indlu yokutshala ikhulisa ukudla kwabasebenzi.", "KOS · Die kweekhuis kweek kos vir die bemanning." } },
            { "equipment.comms", new[] { "RADIO · Communications let the outpost talk to Earth.", "UMSAKAZO · Ukuxhumana kusiza isiteshi sikhulume noMhlaba.", "RADIO · Kommunikasie laat die buitepos met die Aarde praat." } },
            { "equipment.previous", new[] { "BACK", "EMUVA", "TERUG" } },
            { "equipment.next", new[] { "NEXT", "LANDELA", "VOLGENDE" } },
            { "hint.completed", new[] { "Nice exploring! Every try helps you learn.", "Kuhle ukuhlola! Ukuzama ngakunye kuyakufundisa.", "Lekker verken! Elke poging help jou leer." } }
        };

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public static string Get(string key, string fallback)
        {
            int language = PlayerSettingsManager.Instance != null ? (int)PlayerSettingsManager.Instance.Language : 0;
            return Lines.TryGetValue(key, out string[] values) && language < values.Length ? values[language] : fallback;
        }
    }
}
