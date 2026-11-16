import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:model_viewer_plus/model_viewer_plus.dart';
import 'package:shared_preferences/shared_preferences.dart';

void main() => runApp(const AstraMobileApp());

const Color ink = Color(0xFF07111F);
const Color panel = Color(0xE6102032);
const Color cyan = Color(0xFF39D9F2);
const Color mint = Color(0xFF6AE6BF);
const Color amber = Color(0xFFFFC66D);
const Color softText = Color(0xFFB9CADA);

class ChoiceOption {
  const ChoiceOption(
    this.title,
    this.subtitle,
    this.result,
    this.lesson, {
    this.oxygen = 0,
    this.water = 0,
    this.food = 0,
    this.power = 0,
    this.recommended = true,
  });

  final String title;
  final String subtitle;
  final String result;
  final String lesson;
  final int oxygen;
  final int water;
  final int food;
  final int power;
  final bool recommended;
}

class MissionStep {
  const MissionStep({
    required this.title,
    required this.prompt,
    required this.options,
    required this.model,
  });
  final String title;
  final String prompt;
  final List<ChoiceOption> options;
  final String model;
}

class AstraMission {
  const AstraMission({
    required this.title,
    required this.planet,
    required this.subtitle,
    required this.briefing,
    required this.lesson,
    required this.source,
    required this.sourceUrl,
    required this.model,
    required this.color,
    required this.steps,
  });
  final String title;
  final String planet;
  final String subtitle;
  final String briefing;
  final String lesson;
  final String source;
  final String sourceUrl;
  final String model;
  final Color color;
  final List<MissionStep> steps;
}

const List<AstraMission> missions = [
  AstraMission(
    title: 'First Light',
    planet: 'MOON',
    subtitle: 'Bring the lunar outpost online',
    briefing:
        'Restore communications, balance power and life support, shelter for a practice alert, then classify a virtual rover sample.',
    lesson:
        'Water recovery, air cleaning and oxygen supply are connected life-support jobs. NASA reports about 90% water recovery aboard the ISS. ASTRA’s meters and outcomes are simplified game values, not operating instructions.',
    source: 'NASA ECLSS',
    sourceUrl:
        'https://www.nasa.gov/reference/environmental-control-and-life-support-systems-eclss/',
    model: 'assets/models/apollo_lander.glb',
    color: cyan,
    steps: [
      MissionStep(
        title: 'Re-establish the Earth link',
        model: 'assets/models/apollo_lander.glb',
        prompt:
            'The outpost has lost its direct communications link. Where should the dish point first?',
        options: [
          ChoiceOption(
            'Earth relay',
            'Aim toward the visible relay direction.',
            'The mission link is restored.',
            'Communications relays pass signals between a spacecraft and Earth.',
          ),
          ChoiceOption(
            'Down at the landing pad',
            'Point toward the nearby surface.',
            'The signal remains blocked.',
            'A dish needs a clear path to a relay or communications target.',
            recommended: false,
          ),
        ],
      ),
      MissionStep(
        title: 'Align the solar array',
        model: 'assets/models/apollo_lander.glb',
        prompt:
            'Compare the fictional outpost battery reading before and after your choice.',
        options: [
          ChoiceOption(
            'Track the Sun',
            'More generation now; charge the battery.',
            'Power rises by 16 game units.',
            'Solar panels generate more when oriented to receive sunlight.',
            power: 16,
          ),
          ChoiceOption(
            'Protect the array',
            'Reduce exposure during the simulated hazard.',
            'Power rises only slightly, but the array stays protected.',
            'Protective decisions can trade short-term output for equipment safety.',
            power: 5,
            recommended: false,
          ),
        ],
      ),
      MissionStep(
        title: 'Inspect life support',
        model: 'assets/models/iss.glb',
        prompt:
            'The readings show air and water reserves. Which system restores usable water from wastewater?',
        options: [
          ChoiceOption(
            'Water recycler',
            'Start the recovery loop.',
            'Water increases by 18 game units; power decreases by 4.',
            'Water recovery filters and processes used water for reuse.',
            water: 18,
            power: -4,
          ),
          ChoiceOption(
            'Communications dish',
            'Try to route water through the antenna.',
            'The water reading does not change.',
            'Communications hardware moves information, not water.',
            recommended: false,
          ),
        ],
      ),
      MissionStep(
        title: 'Restore breathing oxygen',
        model: 'assets/models/iss.glb',
        prompt:
            'The outpost oxygen meter is low. Choose the system that can add oxygen to the habitat.',
        options: [
          ChoiceOption(
            'Oxygen generator',
            'Run the air revitalization equipment.',
            'Oxygen increases by 16 game units; power decreases by 5.',
            'Real life-support systems manage air and oxygen through several connected processes.',
            oxygen: 16,
            power: -5,
          ),
          ChoiceOption(
            'Solar array',
            'Try to generate oxygen with sunlight.',
            'The battery charges, but oxygen does not increase.',
            'Power enables equipment, but the array itself does not produce breathable air.',
            power: 4,
            recommended: false,
          ),
        ],
      ),
      MissionStep(
        title: 'Prepare for the alert',
        model: 'assets/models/z2_suit.glb',
        prompt:
            'Mission control announces a simulated radiation alert. What should the crew do before the drill begins?',
        options: [
          ChoiceOption(
            'Move into the protected area',
            'Pause outdoor work and account for the crew.',
            'Crew safety is confirmed; power decreases by 3 game units.',
            'Shelter decisions reduce time spent in an exposed area. The game does not predict real radiation dose.',
            power: -3,
          ),
          ChoiceOption(
            'Continue the outdoor task',
            'Finish the job before sheltering.',
            'The crew is late to the protected area; the drill costs 8 game units.',
            'Delaying protective actions can leave the crew exposed longer in this simulation.',
            oxygen: -2,
            power: -8,
            recommended: false,
          ),
        ],
      ),
      MissionStep(
        title: 'Drive to the sample site',
        model: 'assets/models/sev_rover.glb',
        prompt: 'Choose how the rover will reach the orange sample marker.',
        options: [
          ChoiceOption(
            'Follow the marked route',
            'Use the mapped, slower path.',
            'The rover reaches the sample site safely; power decreases by 4.',
            'A mapped route trades a little time and energy for lower terrain risk.',
            power: -4,
          ),
          ChoiceOption(
            'Take the direct shortcut',
            'Use the shortest path.',
            'The rover saves time but loses 8 game units of power on rough ground.',
            'Shortcuts can increase energy use when terrain slows the vehicle.',
            power: -8,
            recommended: false,
          ),
        ],
      ),
      MissionStep(
        title: 'Classify the virtual sample',
        model: 'assets/models/sev_rover.glb',
        prompt:
            'The rover collected dry, loose surface grains. What can you record from this sample?',
        options: [
          ChoiceOption(
            'Loose lunar regolith',
            'Describe the material observed at the surface.',
            'Sample logged as loose regolith.',
            'The classification matches the collected surface material.',
          ),
          ChoiceOption(
            'Liquid water',
            'Claim the sample is flowing water.',
            'The evidence does not support this claim.',
            'A dry surface-grain sample cannot be classified as liquid water.',
            recommended: false,
          ),
        ],
      ),
    ],
  ),
  AstraMission(
    title: 'Moon Greenhouse',
    planet: 'MOON',
    subtitle: 'Grow food for a long stay',
    briefing:
        'Choose a crop plan, manage light and water, watch plants develop, harvest twice, then compare your yields.',
    lesson:
        'NASA studies plant growth systems as a way to provide fresh food and support future long-duration missions. ASTRA’s crop names, timing, yields and costs are fictional game values, not growing instructions.',
    source: 'NASA Space Crops',
    sourceUrl: 'https://science.nasa.gov/biological-physical/space-crops/',
    model: 'assets/models/iss.glb',
    color: mint,
    steps: [
      MissionStep(
        title: 'Choose your crop plan',
        model: 'assets/models/iss.glb',
        prompt:
            'You have water for one initial planting. Which crop strategy will you test?',
        options: [
          ChoiceOption(
            'Fast leafy greens',
            'Shorter growth cycle; modest harvest.',
            'Plan selected: faster first harvest, lower total yield.',
            'Fast-growing crops can return food sooner but may provide less total yield.',
            water: -5,
            food: 3,
          ),
          ChoiceOption(
            'Hardy beans',
            'Longer cycle; larger potential harvest.',
            'Plan selected: higher yield potential, more time and water.',
            'A higher-yield crop can demand more resources and longer growth time.',
            water: -8,
            power: -2,
          ),
        ],
      ),
      MissionStep(
        title: 'Set the light level',
        model: 'assets/models/iss.glb',
        prompt:
            'The plants need light, but the habitat battery is limited. Choose a setting.',
        options: [
          ChoiceOption(
            'Balanced light',
            'Steady growth with moderate energy use.',
            'Growth improves and power decreases by 3.',
            'Plant lighting competes with other powered habitat systems.',
            power: -3,
            food: 2,
          ),
          ChoiceOption(
            'Maximum light',
            'Boost growth now; spend more battery.',
            'Growth is faster; power decreases by 8.',
            'More light can speed growth but increases energy demand.',
            power: -8,
            food: 4,
            recommended: false,
          ),
        ],
      ),
      MissionStep(
        title: 'Manage the water reserve',
        model: 'assets/models/iss.glb',
        prompt:
            'Your water meter is falling. How will you support the crop without draining the reserve?',
        options: [
          ChoiceOption(
            'Recover and reuse water',
            'Route recovered water back to the grow tray.',
            'Water increases by 8; power decreases by 3.',
            'Closed-loop recovery helps stretch limited water supplies.',
            water: 8,
            power: -3,
          ),
          ChoiceOption(
            'Use the emergency reserve',
            'Water the crop immediately.',
            'Plants get water now; the reserve drops by 10.',
            'A reserve can protect growth but leaves less water for other needs.',
            water: -10,
            food: 3,
            recommended: false,
          ),
        ],
      ),
      MissionStep(
        title: 'Observe the first growth cycle',
        model: 'assets/models/iss.glb',
        prompt:
            'The habitat has reached the first observation day. What should the crew record?',
        options: [
          ChoiceOption(
            'Record growth and resource use',
            'Compare plant growth with light and water.',
            'Observation logged; first harvest is ready.',
            'Recording inputs lets the crew compare conditions with the harvest.',
            food: 4,
          ),
          ChoiceOption(
            'Change every setting at once',
            'Turn all controls to maximum.',
            'You cannot tell which change affected growth.',
            'Changing one or two conditions at a time makes comparisons clearer.',
            power: -4,
            recommended: false,
          ),
        ],
      ),
      MissionStep(
        title: 'Harvest and compare',
        model: 'assets/models/iss.glb',
        prompt: 'The first crop is ready. Choose how to use it.',
        options: [
          ChoiceOption(
            'Weigh and log the harvest',
            'Record yield before starting the next cycle.',
            'Harvest one logged. The second planting is ready.',
            'A measured harvest gives a fair comparison between crop plans.',
            food: 12,
          ),
          ChoiceOption(
            'Use it without recording',
            'Feed the crew now.',
            'The crew eats, but the comparison data is incomplete.',
            'A yield comparison needs consistent measurements.',
            food: 8,
            recommended: false,
          ),
        ],
      ),
      MissionStep(
        title: 'Tune the second cycle',
        model: 'assets/models/iss.glb',
        prompt:
            'Use what you learned from the first harvest to adjust your growing plan.',
        options: [
          ChoiceOption(
            'Keep the balanced settings',
            'Repeat the setup for a fair comparison.',
            'Second cycle grows steadily; power decreases by 3.',
            'Keeping conditions steady helps compare the crop choice itself.',
            power: -3,
            food: 3,
          ),
          ChoiceOption(
            'Increase light and water',
            'Try to maximize yield.',
            'Potential yield rises, but water and battery fall faster.',
            'A larger harvest can cost resources needed by the rest of the habitat.',
            water: -6,
            power: -7,
            food: 6,
            recommended: false,
          ),
        ],
      ),
      MissionStep(
        title: 'Complete the second harvest',
        model: 'assets/models/iss.glb',
        prompt:
            'Measure the second crop and compare it with your first result.',
        options: [
          ChoiceOption(
            'Log both yields',
            'Compare food gained against water and power used.',
            'Two harvests logged. Greenhouse study complete.',
            'Food production requires tradeoffs across harvest, water, light and energy.',
            food: 13,
          ),
          ChoiceOption(
            'Record only the larger number',
            'Skip the resource comparison.',
            'You have a harvest total, but cannot compare efficiency.',
            'Yield alone does not show the resource cost of growing food.',
            food: 10,
            recommended: false,
          ),
        ],
      ),
    ],
  ),
  AstraMission(
    title: 'Storm Watch',
    planet: 'MARS',
    subtitle: 'Protect the crew during a drill',
    briefing:
        'Read the event briefing, check outpost readiness, pause outdoor work, shelter the crew, then inspect and report.',
    lesson:
        'NASA DONKI provides space-weather event observations and context. It does not predict a Mars surface dose or crew medical outcome. ASTRA’s alert, countdown and resource changes are simulated training values.',
    source: 'NASA CCMC DONKI',
    sourceUrl: 'https://ccmc.gsfc.nasa.gov/tools/DONKI/',
    model: 'assets/models/perseverance.glb',
    color: amber,
    steps: [
      MissionStep(
        title: 'Read the event briefing',
        model: 'assets/models/perseverance.glb',
        prompt:
            'The report describes a solar energetic-particle event. What does this briefing tell your crew?',
        options: [
          ChoiceOption(
            'A monitored space-weather event',
            'Use it as a reason to review readiness.',
            'Report reviewed. The drill does not provide a Mars dose estimate.',
            'DONKI summarizes observed and analyzed events; it is not a crew dose forecast.',
            recommended: true,
          ),
          ChoiceOption(
            'A precise Mars surface dose',
            'Treat the event record as a local dose reading.',
            'That conclusion is not supported by this report.',
            'An event record alone cannot establish radiation levels at a specific Mars outpost.',
            recommended: false,
          ),
        ],
      ),
      MissionStep(
        title: 'Check outpost readiness',
        model: 'assets/models/hdu.glb',
        prompt:
            'Before the simulated alert, what should the crew verify first?',
        options: [
          ChoiceOption(
            'Power, air, comms and shelter',
            'Confirm the essential systems are ready.',
            'Readiness checks complete; power decreases by 2.',
            'A preparedness check looks at the systems the crew depends on.',
            power: -2,
          ),
          ChoiceOption(
            'Rover speed only',
            'Prepare to continue outdoor work.',
            'The outpost systems remain unchecked.',
            'The rover cannot protect the crew or keep the habitat running by itself.',
            recommended: false,
          ),
        ],
      ),
      MissionStep(
        title: 'Decide when to pause outdoor work',
        model: 'assets/models/perseverance.glb',
        prompt:
            'The drill alert arrives while a rover traverse is planned. What do you do?',
        options: [
          ChoiceOption(
            'Suspend the traverse now',
            'Bring the crew back before the simulated event.',
            'Mission time increases by 4 minutes; the crew reaches shelter sooner.',
            'Pausing costs schedule time but reduces time spent outside during the drill.',
            power: -2,
          ),
          ChoiceOption(
            'Finish the traverse first',
            'Keep the schedule and return later.',
            'You save 2 minutes, but lose 9 game units of power in the simulated event.',
            'Continuing outdoor work can increase the simulated cost to mission resources.',
            power: -9,
            recommended: false,
          ),
        ],
      ),
      MissionStep(
        title: 'Guide the crew to protection',
        model: 'assets/models/z2_suit.glb',
        prompt: 'The simulated event is active. Where should the astronaut go?',
        options: [
          ChoiceOption(
            'Designated protected area',
            'Account for the crew and close the shelter.',
            'Crew sheltered. Habitat power decreases by 3 game units.',
            'The drill teaches a response sequence; it is not a real protection or dose calculation.',
            power: -3,
          ),
          ChoiceOption(
            'Stay by the rover',
            'Keep the crew outside near the equipment.',
            'Crew remains outside; life support and power lose 6 game units.',
            'Equipment recovery does not replace crew protection in this exercise.',
            oxygen: -2,
            power: -6,
            recommended: false,
          ),
        ],
      ),
      MissionStep(
        title: 'Inspect the outpost after the drill',
        model: 'assets/models/hdu.glb',
        prompt:
            'The event has ended. What should the post-drill inspection include?',
        options: [
          ChoiceOption(
            'Power, life support, comms and shelter',
            'Record each system and note crew status.',
            'Inspection completed. All four areas are logged.',
            'A post-event inspection catches problems before outdoor operations resume.',
          ),
          ChoiceOption(
            'Only restart the rover',
            'Skip the habitat checks.',
            'The mission report is incomplete.',
            'Outdoor work should not resume until essential habitat systems are checked.',
            recommended: false,
          ),
        ],
      ),
      MissionStep(
        title: 'Submit the debrief',
        model: 'assets/models/hdu.glb',
        prompt: 'What is the key lesson from this simulated event?',
        options: [
          ChoiceOption(
            'Protect the crew, then inspect systems',
            'Use reports for context, not unsupported dose claims.',
            'Debrief submitted. Mars crew drill complete.',
            'A report, a measured local exposure, and a medical forecast are different kinds of evidence.',
          ),
          ChoiceOption(
            'The report gives an exact crew dose',
            'Treat the simulation as a real forecast.',
            'That would overstate what the data can tell you.',
            'ASTRA is a learning simulation; it does not estimate real astronaut dose.',
            recommended: false,
          ),
        ],
      ),
    ],
  ),
  AstraMission(
    title: 'Shadow Scout',
    planet: 'EUROPA',
    subtitle: 'Survey an icy moon carefully',
    briefing:
        'Choose a rover route, inspect two exposed surface sites, collect a virtual sample and state what the evidence can—and cannot—show.',
    lesson:
        'A surface sample can describe exposed surface material. It cannot establish the composition of Europa’s subsurface ocean. ASTRA’s route and sample are simulated and do not claim a real discovery.',
    source: 'NASA Europa Clipper',
    sourceUrl: 'https://science.nasa.gov/mission/europa-clipper/',
    model: 'assets/models/sev_rover.glb',
    color: Color(0xFF9ABEFF),
    steps: [
      MissionStep(
        title: 'Establish the survey link',
        model: 'assets/models/sev_rover.glb',
        prompt:
            'The scout rover needs a clear relay before it crosses the icy terrain. Choose a link plan.',
        options: [
          ChoiceOption(
            'Relay through the outpost',
            'Confirm signal before leaving.',
            'Telemetry link is active; power decreases by 2.',
            'A relay keeps the rover connected while it explores.',
            power: -2,
          ),
          ChoiceOption(
            'Explore without telemetry',
            'Start the traverse immediately.',
            'The rover starts, but the team loses live status.',
            'A communications gap makes it harder to monitor a remote vehicle.',
            recommended: false,
          ),
        ],
      ),
      MissionStep(
        title: 'Plan a route around the ice',
        model: 'assets/models/sev_rover.glb',
        prompt:
            'The map shows a longer stable ridge route and a shorter rough crossing.',
        options: [
          ChoiceOption(
            'Follow the stable ridge',
            'Spend more time; protect the rover.',
            'The rover reaches the survey area; power decreases by 4.',
            'Route planning balances terrain risk, travel time and energy.',
            power: -4,
          ),
          ChoiceOption(
            'Cross the rough patch',
            'Take the direct route.',
            'The rover arrives faster but loses 10 game units of power.',
            'A short route can be less efficient on difficult ground.',
            power: -10,
            recommended: false,
          ),
        ],
      ),
      MissionStep(
        title: 'Inspect the first surface site',
        model: 'assets/models/sev_rover.glb',
        prompt:
            'Which observation is useful before selecting a sample location?',
        options: [
          ChoiceOption(
            'Record visible surface texture',
            'Compare the exposed ice and nearby terrain.',
            'Site A observation saved.',
            'Remote observations help teams choose where to sample.',
            food: 0,
          ),
          ChoiceOption(
            'Assume the hidden layers',
            'Claim what lies below the surface.',
            'The rover camera cannot see beneath the ice.',
            'A surface image is not evidence of subsurface composition.',
            recommended: false,
          ),
        ],
      ),
      MissionStep(
        title: 'Choose a sample site',
        model: 'assets/models/sev_rover.glb',
        prompt:
            'Two accessible patches are marked. Where should the rover collect its virtual sample?',
        options: [
          ChoiceOption(
            'Exposed, well-lit patch',
            'Choose a site the camera can document clearly.',
            'Site B selected; sample camera and context are logged.',
            'Clear documentation helps researchers understand where a sample came from.',
          ),
          ChoiceOption(
            'Unmapped shadowed crevice',
            'Collect where the terrain is unobserved.',
            'The site is hard to document and the route is risky.',
            'A sample without site context is harder to interpret.',
            power: -4,
            recommended: false,
          ),
        ],
      ),
      MissionStep(
        title: 'Classify the virtual sample',
        model: 'assets/models/sev_rover.glb',
        prompt:
            'The instrument identifies exposed water ice at the surface. What is a careful conclusion?',
        options: [
          ChoiceOption(
            'Surface water ice',
            'Describe the measured exposed material.',
            'Sample logged as surface ice.',
            'The observation supports a surface-ice classification.',
          ),
          ChoiceOption(
            'Ocean composition confirmed',
            'Claim the sample came from below the ice shell.',
            'The sample cannot establish this.',
            'A surface sample cannot directly prove the subsurface ocean’s composition.',
            recommended: false,
          ),
        ],
      ),
      MissionStep(
        title: 'Write the science note',
        model: 'assets/models/sev_rover.glb',
        prompt:
            'Finish the report with a statement that stays within the evidence.',
        options: [
          ChoiceOption(
            'Surface ice observed; ocean unknown',
            'State both the observation and its limit.',
            'Evidence note saved. Europa survey complete.',
            'Careful science separates a measured surface observation from an untested subsurface claim.',
          ),
          ChoiceOption(
            'Life found under the ice',
            'Make a discovery claim from this surface sample.',
            'The evidence does not support that claim.',
            'This simulation cannot detect life or sample Europa’s ocean.',
            recommended: false,
          ),
        ],
      ),
    ],
  ),
];

class AstraMobileApp extends StatelessWidget {
  const AstraMobileApp({super.key});

  @override
  Widget build(BuildContext context) => MaterialApp(
    title: 'ASTRA Mission Control',
    debugShowCheckedModeBanner: false,
    theme: ThemeData(
      useMaterial3: true,
      brightness: Brightness.dark,
      scaffoldBackgroundColor: ink,
      colorScheme: ColorScheme.fromSeed(
        seedColor: cyan,
        brightness: Brightness.dark,
      ),
      fontFamily: 'Roboto',
      textTheme: const TextTheme(
        headlineMedium: TextStyle(
          fontSize: 30,
          fontWeight: FontWeight.w800,
          height: 1.08,
        ),
        titleLarge: TextStyle(fontSize: 22, fontWeight: FontWeight.w800),
        bodyLarge: TextStyle(fontSize: 17, height: 1.42),
        bodyMedium: TextStyle(fontSize: 15, height: 1.4),
      ),
    ),
    home: const AstraHome(),
  );
}

class AstraHome extends StatefulWidget {
  const AstraHome({super.key});

  @override
  State<AstraHome> createState() => _AstraHomeState();
}

class _AstraHomeState extends State<AstraHome> {
  int page = 0;
  int missionIndex = 0;
  int stepIndex = 0;
  int oxygen = 84;
  int water = 72;
  int food = 66;
  int power = 58;
  int goodChoices = 0;
  int textScale = 0;
  bool highContrast = false;
  bool subtitles = true;
  List<int> completed = [];
  ChoiceOption? selected;
  bool booting = true;

  AstraMission get mission => missions[missionIndex];
  MissionStep get step => mission.steps[stepIndex];

  @override
  void initState() {
    super.initState();
    _loadProgress();
  }

  Future<void> _loadProgress() async {
    final prefs = await SharedPreferences.getInstance();
    if (!mounted) return;
    setState(() {
      completed =
          prefs.getStringList('astra.completed')?.map(int.parse).toList() ?? [];
      textScale = prefs.getInt('astra.textScale') ?? 0;
      highContrast = prefs.getBool('astra.highContrast') ?? false;
      subtitles = prefs.getBool('astra.subtitles') ?? true;
      booting = false;
    });
  }

  Future<void> _saveSettings() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setStringList(
      'astra.completed',
      completed.map((e) => '$e').toList(),
    );
    await prefs.setInt('astra.textScale', textScale);
    await prefs.setBool('astra.highContrast', highContrast);
    await prefs.setBool('astra.subtitles', subtitles);
  }

  bool isUnlocked(int index) => index == 0 || completed.contains(index - 1);

  void showBoard() => setState(() => page = 1);

  void openMission(int index) {
    if (!isUnlocked(index)) return;
    setState(() {
      missionIndex = index;
      page = 2;
    });
  }

  void startMission() {
    setState(() {
      page = 3;
      stepIndex = 0;
      selected = null;
      goodChoices = 0;
      oxygen = 84;
      water = 72;
      food = 66;
      power = 58;
    });
  }

  int _clamp(int value) => value.clamp(0, 100).toInt();

  void choose(ChoiceOption choice) {
    setState(() {
      selected = choice;
      oxygen = _clamp(oxygen + choice.oxygen);
      water = _clamp(water + choice.water);
      food = _clamp(food + choice.food);
      power = _clamp(power + choice.power);
      if (choice.recommended) goodChoices++;
    });
  }

  void continueStep() {
    if (stepIndex + 1 < mission.steps.length) {
      setState(() {
        stepIndex++;
        selected = null;
      });
    } else {
      _finishMission();
    }
  }

  Future<void> _finishMission() async {
    if (!completed.contains(missionIndex)) completed.add(missionIndex);
    await _saveSettings();
    if (mounted) setState(() => page = 4);
  }

  Future<void> nextMission() async {
    final next = missionIndex + 1;
    if (next >= missions.length) {
      setState(() => page = 1);
      return;
    }
    setState(() {
      missionIndex = next;
      page = 2;
    });
  }

  @override
  Widget build(BuildContext context) {
    if (booting)
      return const Scaffold(
        backgroundColor: ink,
        body: Center(child: CircularProgressIndicator()),
      );
    return MediaQuery(
      data: MediaQuery.of(
        context,
      ).copyWith(textScaler: TextScaler.linear(1 + textScale * .12)),
      child: Scaffold(
        backgroundColor: ink,
        body: SpaceBackdrop(
          child: SafeArea(
            child: Column(children: [_topBar(), Expanded(child: _pageBody())]),
          ),
        ),
      ),
    );
  }

  Widget _topBar() => Padding(
    padding: const EdgeInsets.fromLTRB(18, 10, 18, 8),
    child: Row(
      children: [
        if (page != 0)
          IconButton.filledTonal(
            tooltip: 'Back',
            onPressed:
                () =>
                    setState(() => page = page == 3 ? 2 : (page == 4 ? 1 : 0)),
            icon: const Icon(Icons.arrow_back_rounded),
          )
        else
          const Icon(Icons.public, color: cyan, size: 28),
        const SizedBox(width: 10),
        const Expanded(
          child: Text(
            'ASTRA  /  MISSION CONTROL',
            style: TextStyle(
              fontWeight: FontWeight.w800,
              letterSpacing: 1.1,
              fontSize: 13,
            ),
          ),
        ),
        IconButton(
          tooltip: 'Accessibility settings',
          onPressed: _openSettings,
          icon: const Icon(Icons.accessibility_new, color: softText),
        ),
      ],
    ),
  );

  Widget _pageBody() {
    switch (page) {
      case 0:
        return _homePage();
      case 1:
        return _missionBoard();
      case 2:
        return _briefingPage();
      case 3:
        return _playPage();
      default:
        return _debriefPage();
    }
  }

  Widget _homePage() => SingleChildScrollView(
    padding: const EdgeInsets.fromLTRB(18, 8, 18, 28),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Text(
          'EXPLORE  •  LEARN  •  ACHIEVE',
          style: TextStyle(
            color: cyan,
            fontWeight: FontWeight.w800,
            letterSpacing: 2,
            fontSize: 12,
          ),
        ),
        const SizedBox(height: 8),
        const Text(
          'Your next mission\nstarts here.',
          style: TextStyle(
            fontSize: 34,
            height: 1.08,
            fontWeight: FontWeight.w900,
          ),
        ),
        const SizedBox(height: 14),
        ModelCard(
          asset: 'assets/models/z2_suit.glb',
          label: 'Meet your astronaut',
          height: 220,
        ),
        const SizedBox(height: 18),
        GlassCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Row(
                children: [
                  Icon(Icons.radar, color: cyan),
                  SizedBox(width: 9),
                  Text(
                    'MISSION CONTROL',
                    style: TextStyle(
                      color: cyan,
                      fontWeight: FontWeight.w900,
                      letterSpacing: 1,
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 9),
              Text(
                completed.isEmpty
                    ? 'Start with First Light on the Moon.'
                    : '${completed.length} of 4 missions complete. Ready to continue?',
                style: const TextStyle(
                  fontSize: 19,
                  fontWeight: FontWeight.w700,
                ),
              ),
              const SizedBox(height: 7),
              const Text(
                'Touch-friendly missions. Make a choice, see what changes, and learn why.',
                style: TextStyle(color: softText),
              ),
              const SizedBox(height: 16),
              PrimaryButton(
                label:
                    completed.isEmpty ? 'START TRAINING' : 'CONTINUE CAMPAIGN',
                icon: Icons.rocket_launch,
                onPressed: showBoard,
              ),
            ],
          ),
        ),
        const SizedBox(height: 16),
        Row(
          children: [
            Expanded(
              child: _smallInfo(
                Icons.touch_app,
                'Made for touch',
                'Big controls',
              ),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: _smallInfo(
                Icons.school_outlined,
                'Learn by doing',
                'NASA sources',
              ),
            ),
          ],
        ),
      ],
    ),
  );

  Widget _smallInfo(IconData icon, String title, String sub) => GlassCard(
    padding: const EdgeInsets.all(14),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(icon, color: mint),
        const SizedBox(height: 8),
        Text(title, style: const TextStyle(fontWeight: FontWeight.w800)),
        Text(sub, style: const TextStyle(color: softText, fontSize: 13)),
      ],
    ),
  );

  Widget _missionBoard() => SingleChildScrollView(
    padding: const EdgeInsets.fromLTRB(18, 4, 18, 28),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Text(
          'CAMPAIGN',
          style: TextStyle(
            color: cyan,
            letterSpacing: 2,
            fontWeight: FontWeight.w900,
          ),
        ),
        const SizedBox(height: 6),
        const Text(
          'Choose your mission',
          style: TextStyle(fontSize: 29, fontWeight: FontWeight.w900),
        ),
        const SizedBox(height: 6),
        const Text(
          'Complete each assignment to unlock the next destination.',
          style: TextStyle(color: softText),
        ),
        const SizedBox(height: 16),
        for (int i = 0; i < missions.length; i++) _missionTile(i),
      ],
    ),
  );

  Widget _missionTile(int i) {
    final item = missions[i];
    final unlocked = isUnlocked(i);
    final done = completed.contains(i);
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Semantics(
        button: unlocked,
        label:
            '${item.title}, ${item.planet}, ${done
                ? 'completed'
                : unlocked
                ? 'unlocked'
                : 'locked'}',
        child: InkWell(
          onTap: unlocked ? () => openMission(i) : null,
          borderRadius: BorderRadius.circular(20),
          child: GlassCard(
            borderColor:
                done
                    ? mint.withValues(alpha: .6)
                    : item.color.withValues(alpha: unlocked ? .45 : .15),
            child: Row(
              children: [
                Container(
                  width: 44,
                  height: 44,
                  alignment: Alignment.center,
                  decoration: BoxDecoration(
                    shape: BoxShape.circle,
                    color: item.color.withValues(alpha: .14),
                    border: Border.all(color: item.color.withValues(alpha: .6)),
                  ),
                  child: Text(
                    '${i + 1}'.padLeft(2, '0'),
                    style: TextStyle(
                      color: item.color,
                      fontWeight: FontWeight.w900,
                    ),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        '${item.planet}  •  ${done
                            ? 'COMPLETE'
                            : unlocked
                            ? 'READY'
                            : 'LOCKED'}',
                        style: TextStyle(
                          color: done ? mint : item.color,
                          fontSize: 11,
                          fontWeight: FontWeight.w900,
                          letterSpacing: 1,
                        ),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        item.title,
                        style: const TextStyle(
                          fontSize: 17,
                          fontWeight: FontWeight.w800,
                        ),
                      ),
                      Text(
                        item.subtitle,
                        style: const TextStyle(color: softText, fontSize: 13),
                      ),
                    ],
                  ),
                ),
                Icon(
                  done
                      ? Icons.check_circle
                      : unlocked
                      ? Icons.chevron_right
                      : Icons.lock_outline,
                  color: done ? mint : softText,
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _briefingPage() => SingleChildScrollView(
    padding: const EdgeInsets.fromLTRB(18, 4, 18, 28),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          'MISSION ${('${missionIndex + 1}').padLeft(2, '0')}  /  ${mission.planet}',
          style: TextStyle(
            color: mission.color,
            fontWeight: FontWeight.w900,
            letterSpacing: 1.5,
          ),
        ),
        const SizedBox(height: 8),
        Text(
          mission.title,
          style: const TextStyle(fontSize: 30, fontWeight: FontWeight.w900),
        ),
        const SizedBox(height: 6),
        Text(
          mission.subtitle,
          style: const TextStyle(color: softText, fontSize: 17),
        ),
        const SizedBox(height: 14),
        ModelCard(
          asset: mission.model,
          label: '${mission.planet} mission hardware',
          height: 210,
        ),
        const SizedBox(height: 14),
        GlassCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const LabelText('YOUR ASSIGNMENT'),
              const SizedBox(height: 8),
              Text(mission.briefing),
              const SizedBox(height: 16),
              const LabelText('WHAT YOU WILL LEARN'),
              const SizedBox(height: 8),
              Text(mission.lesson, style: const TextStyle(color: softText)),
              const SizedBox(height: 12),
              Text(
                'SOURCE  •  ${mission.source}',
                style: const TextStyle(
                  color: cyan,
                  fontSize: 12,
                  fontWeight: FontWeight.w800,
                ),
              ),
            ],
          ),
        ),
        const SizedBox(height: 16),
        PrimaryButton(
          label: 'BEGIN MISSION',
          icon: Icons.play_arrow_rounded,
          onPressed: startMission,
        ),
      ],
    ),
  );

  Widget _playPage() => Column(
    children: [
      Padding(
        padding: const EdgeInsets.fromLTRB(18, 2, 18, 8),
        child: Column(
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    '${mission.title.toUpperCase()}  •  ${mission.planet}',
                    style: TextStyle(
                      color: mission.color,
                      fontSize: 12,
                      fontWeight: FontWeight.w900,
                      letterSpacing: .8,
                    ),
                  ),
                ),
                Text(
                  '${stepIndex + 1}/${mission.steps.length}',
                  style: const TextStyle(
                    color: softText,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 9),
            ClipRRect(
              borderRadius: BorderRadius.circular(20),
              child: LinearProgressIndicator(
                value:
                    (stepIndex + (selected == null ? 0 : 1)) /
                    mission.steps.length,
                minHeight: 7,
                color: cyan,
                backgroundColor: Colors.white12,
              ),
            ),
            const SizedBox(height: 9),
            ResourceBar(oxygen: oxygen, water: water, food: food, power: power),
          ],
        ),
      ),
      Expanded(
        child: SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(18, 4, 18, 14),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              ModelCard(asset: step.model, label: 'Mission scene', height: 178),
              const SizedBox(height: 13),
              GlassCard(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'STEP ${stepIndex + 1}',
                      style: TextStyle(
                        color: mission.color,
                        fontSize: 11,
                        letterSpacing: 1.6,
                        fontWeight: FontWeight.w900,
                      ),
                    ),
                    const SizedBox(height: 6),
                    Text(
                      step.title,
                      style: const TextStyle(
                        fontSize: 23,
                        fontWeight: FontWeight.w900,
                      ),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      step.prompt,
                      style: const TextStyle(fontSize: 16, color: softText),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 12),
              for (final choice in step.options) _choiceButton(choice),
              if (selected != null) ...[
                const SizedBox(height: 8),
                GlassCard(
                  borderColor:
                      selected!.recommended
                          ? mint.withValues(alpha: .7)
                          : amber.withValues(alpha: .7),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          Icon(
                            selected!.recommended
                                ? Icons.check_circle
                                : Icons.info_outline,
                            color: selected!.recommended ? mint : amber,
                          ),
                          const SizedBox(width: 8),
                          Text(
                            selected!.recommended
                                ? 'OUTCOME'
                                : 'TRY A DIFFERENT PLAN',
                            style: TextStyle(
                              color: selected!.recommended ? mint : amber,
                              fontWeight: FontWeight.w900,
                              letterSpacing: 1,
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 8),
                      Text(
                        selected!.result,
                        style: const TextStyle(fontWeight: FontWeight.w800),
                      ),
                      const SizedBox(height: 6),
                      Text(
                        selected!.lesson,
                        style: const TextStyle(color: softText),
                      ),
                    ],
                  ),
                ),
              ],
            ],
          ),
        ),
      ),
      Padding(
        padding: const EdgeInsets.fromLTRB(18, 4, 18, 14),
        child:
            selected == null
                ? const Text(
                  'Choose an action to continue',
                  textAlign: TextAlign.center,
                  style: TextStyle(color: softText),
                )
                : PrimaryButton(
                  label:
                      stepIndex + 1 == mission.steps.length
                          ? 'COMPLETE MISSION'
                          : 'NEXT STEP',
                  icon: Icons.arrow_forward_rounded,
                  onPressed: continueStep,
                ),
      ),
    ],
  );

  Widget _choiceButton(ChoiceOption choice) {
    final isChosen = selected == choice;
    return Padding(
      padding: const EdgeInsets.only(bottom: 9),
      child: Semantics(
        button: true,
        selected: isChosen,
        label: '${choice.title}. ${choice.subtitle}',
        child: InkWell(
          onTap: selected == null ? () => choose(choice) : null,
          borderRadius: BorderRadius.circular(16),
          child: AnimatedContainer(
            duration: const Duration(milliseconds: 180),
            width: double.infinity,
            padding: const EdgeInsets.symmetric(horizontal: 15, vertical: 14),
            decoration: BoxDecoration(
              color: isChosen ? cyan.withValues(alpha: .16) : panel,
              borderRadius: BorderRadius.circular(16),
              border: Border.all(
                color: isChosen ? cyan : Colors.white24,
                width: isChosen ? 1.5 : 1,
              ),
            ),
            child: Row(
              children: [
                Icon(
                  isChosen
                      ? Icons.radio_button_checked
                      : Icons.radio_button_unchecked,
                  color: isChosen ? cyan : softText,
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        choice.title,
                        style: const TextStyle(
                          fontSize: 16,
                          fontWeight: FontWeight.w800,
                        ),
                      ),
                      const SizedBox(height: 3),
                      Text(
                        choice.subtitle,
                        style: const TextStyle(color: softText, fontSize: 13),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _debriefPage() {
    final score = ((goodChoices / mission.steps.length) * 100).round();
    final hasNext = missionIndex + 1 < missions.length;
    return SingleChildScrollView(
      padding: const EdgeInsets.fromLTRB(18, 12, 18, 30),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Icon(Icons.workspace_premium_outlined, size: 54, color: amber),
          const SizedBox(height: 12),
          const Text(
            'MISSION COMPLETE',
            style: TextStyle(
              color: mint,
              fontWeight: FontWeight.w900,
              letterSpacing: 2,
            ),
          ),
          const SizedBox(height: 7),
          Text(
            mission.title,
            style: const TextStyle(fontSize: 32, fontWeight: FontWeight.w900),
          ),
          const SizedBox(height: 13),
          GlassCard(
            child: Row(
              children: [
                const Expanded(
                  child: Text(
                    'MISSION SCORE',
                    style: TextStyle(
                      color: softText,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                ),
                Text(
                  '$score',
                  style: const TextStyle(
                    color: amber,
                    fontSize: 34,
                    fontWeight: FontWeight.w900,
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 14),
          ModelCard(
            asset: mission.model,
            label: 'Mission completed',
            height: 200,
          ),
          const SizedBox(height: 14),
          GlassCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const LabelText('WHAT YOU LEARNED'),
                const SizedBox(height: 8),
                Text(mission.lesson),
              ],
            ),
          ),
          const SizedBox(height: 16),
          PrimaryButton(
            label:
                hasNext
                    ? 'BRIEF ${missions[missionIndex + 1].planet} MISSION'
                    : 'RETURN TO CAMPAIGN',
            icon: hasNext ? Icons.rocket_launch : Icons.flag_rounded,
            onPressed: hasNext ? nextMission : showBoard,
          ),
          const SizedBox(height: 10),
          OutlinedButton.icon(
            onPressed: showBoard,
            icon: const Icon(Icons.grid_view),
            label: const Text('MISSION BOARD'),
            style: OutlinedButton.styleFrom(
              minimumSize: const Size.fromHeight(52),
              foregroundColor: softText,
            ),
          ),
        ],
      ),
    );
  }

  Future<void> _openSettings() async {
    await showModalBottomSheet<void>(
      context: context,
      backgroundColor: const Color(0xFF102033),
      isScrollControlled: true,
      builder:
          (context) => StatefulBuilder(
            builder:
                (context, sheetSet) => SafeArea(
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(20, 20, 20, 28),
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Text(
                          'MAKE ASTRA FIT YOU',
                          style: TextStyle(
                            fontWeight: FontWeight.w900,
                            fontSize: 22,
                          ),
                        ),
                        const SizedBox(height: 14),
                        SwitchListTile(
                          title: const Text('High contrast'),
                          value: highContrast,
                          onChanged: (v) {
                            setState(() => highContrast = v);
                            sheetSet(() {});
                            _saveSettings();
                          },
                        ),
                        SwitchListTile(
                          title: const Text('Mission captions'),
                          value: subtitles,
                          onChanged: (v) {
                            setState(() => subtitles = v);
                            sheetSet(() {});
                            _saveSettings();
                          },
                        ),
                        Row(
                          children: [
                            const Expanded(child: Text('Text size')),
                            IconButton(
                              tooltip: 'Smaller text',
                              onPressed:
                                  textScale == 0
                                      ? null
                                      : () {
                                        setState(() => textScale--);
                                        sheetSet(() {});
                                        _saveSettings();
                                      },
                              icon: const Icon(Icons.text_decrease),
                            ),
                            IconButton(
                              tooltip: 'Larger text',
                              onPressed:
                                  textScale == 3
                                      ? null
                                      : () {
                                        setState(() => textScale++);
                                        sheetSet(() {});
                                        _saveSettings();
                                      },
                              icon: const Icon(Icons.text_increase),
                            ),
                          ],
                        ),
                        const Text(
                          'Controls are designed for touch. Text and contrast settings are saved on this device.',
                          style: TextStyle(color: softText),
                        ),
                      ],
                    ),
                  ),
                ),
          ),
    );
  }
}

class ResourceBar extends StatelessWidget {
  const ResourceBar({
    super.key,
    required this.oxygen,
    required this.water,
    required this.food,
    required this.power,
  });
  final int oxygen, water, food, power;
  @override
  Widget build(BuildContext context) => GlassCard(
    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 10),
    child: Row(
      children: [
        Expanded(child: _meter('O₂', oxygen, cyan)),
        Expanded(child: _meter('H₂O', water, const Color(0xFF6CB7FF))),
        Expanded(child: _meter('FOOD', food, mint)),
        Expanded(child: _meter('POWER', power, amber)),
      ],
    ),
  );
  Widget _meter(String name, int value, Color color) => Padding(
    padding: const EdgeInsets.symmetric(horizontal: 4),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Text(
              name,
              style: TextStyle(
                color: color,
                fontSize: 10,
                fontWeight: FontWeight.w900,
              ),
            ),
            const Spacer(),
            Text(
              '$value%',
              style: const TextStyle(fontSize: 10, fontWeight: FontWeight.w800),
            ),
          ],
        ),
        const SizedBox(height: 5),
        ClipRRect(
          borderRadius: BorderRadius.circular(10),
          child: LinearProgressIndicator(
            value: value / 100,
            minHeight: 5,
            color: color,
            backgroundColor: Colors.white12,
          ),
        ),
      ],
    ),
  );
}

class ModelCard extends StatelessWidget {
  const ModelCard({
    super.key,
    required this.asset,
    required this.label,
    required this.height,
  });
  final String asset;
  final String label;
  final double height;
  @override
  Widget build(BuildContext context) => ClipRRect(
    borderRadius: BorderRadius.circular(20),
    child: Container(
      height: height,
      decoration: BoxDecoration(
        gradient: const LinearGradient(
          colors: [Color(0xFF10263B), Color(0xFF07111F)],
        ),
        border: Border.all(color: cyan.withValues(alpha: .35)),
        borderRadius: BorderRadius.circular(20),
      ),
      child: Stack(
        children: [
          Positioned.fill(
            child: ModelViewer(
              src: asset,
              alt: label,
              ar: false,
              autoRotate: true,
              cameraControls: true,
              disableZoom: false,
              backgroundColor: ink,
              shadowIntensity: 0.8,
              exposure: 0.9,
            ),
          ),
          Positioned(
            left: 12,
            top: 10,
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
              decoration: BoxDecoration(
                color: ink.withValues(alpha: .8),
                borderRadius: BorderRadius.circular(20),
              ),
              child: Text(
                label.toUpperCase(),
                style: const TextStyle(
                  color: cyan,
                  fontSize: 10,
                  letterSpacing: 1,
                  fontWeight: FontWeight.w900,
                ),
              ),
            ),
          ),
        ],
      ),
    ),
  );
}

class GlassCard extends StatelessWidget {
  const GlassCard({
    super.key,
    required this.child,
    this.padding = const EdgeInsets.all(16),
    this.borderColor = const Color(0x5039D9F2),
  });
  final Widget child;
  final EdgeInsets padding;
  final Color borderColor;
  @override
  Widget build(BuildContext context) => Container(
    width: double.infinity,
    padding: padding,
    decoration: BoxDecoration(
      color: highContrastColor(context),
      borderRadius: BorderRadius.circular(19),
      border: Border.all(color: borderColor),
    ),
    child: child,
  );

  Color highContrastColor(BuildContext context) {
    final state = context.findAncestorStateOfType<_AstraHomeState>();
    return state?.highContrast == true ? const Color(0xFF000000) : panel;
  }
}

class PrimaryButton extends StatelessWidget {
  const PrimaryButton({
    super.key,
    required this.label,
    required this.icon,
    required this.onPressed,
  });
  final String label;
  final IconData icon;
  final VoidCallback onPressed;
  @override
  Widget build(BuildContext context) => SizedBox(
    width: double.infinity,
    height: 56,
    child: FilledButton.icon(
      onPressed: onPressed,
      icon: Icon(icon),
      label: Text(
        label,
        style: const TextStyle(fontWeight: FontWeight.w900, letterSpacing: .7),
      ),
      style: FilledButton.styleFrom(
        backgroundColor: const Color(0xFF047C91),
        foregroundColor: Colors.white,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
      ),
    ),
  );
}

class LabelText extends StatelessWidget {
  const LabelText(this.text, {super.key});
  final String text;
  @override
  Widget build(BuildContext context) => Text(
    text,
    style: const TextStyle(
      color: cyan,
      fontSize: 12,
      letterSpacing: 1.3,
      fontWeight: FontWeight.w900,
    ),
  );
}

class SpaceBackdrop extends StatelessWidget {
  const SpaceBackdrop({super.key, required this.child});
  final Widget child;
  @override
  Widget build(BuildContext context) => Stack(
    children: [
      const Positioned.fill(
        child: DecoratedBox(
          decoration: BoxDecoration(
            gradient: LinearGradient(
              begin: Alignment.topCenter,
              end: Alignment.bottomCenter,
              colors: [Color(0xFF07111F), Color(0xFF0B1C2D), Color(0xFF08121E)],
            ),
          ),
        ),
      ),
      const Positioned.fill(child: CustomPaint(painter: StarPainter())),
      child,
    ],
  );
}

class StarPainter extends CustomPainter {
  const StarPainter();
  @override
  void paint(Canvas canvas, Size size) {
    final random = math.Random(23);
    final paint = Paint()..color = const Color(0x777DDBF0);
    for (var i = 0; i < 75; i++) {
      final x = random.nextDouble() * size.width;
      final y = random.nextDouble() * size.height;
      final radius = random.nextDouble() * 1.2 + .25;
      canvas.drawCircle(Offset(x, y), radius, paint);
    }
  }

  @override
  bool shouldRepaint(covariant StarPainter oldDelegate) => false;
}
