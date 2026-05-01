/**
 * Sample posts for local dev — mirrors the PostDocument shape from Cosmos DB.
 * Each post has exactly 5 structured facts with images.
 * When real Cosmos data is wired in (Phase 4), this file is removed.
 */

export interface MockFact {
  title: string;
  body: string;
  imageUrl: string;
  imageAlt: string;
}

export interface MockPost {
  id: string;
  issueNumber: number;
  slug: string;
  /** Canonical URL used for <link rel="canonical"> and og:url */
  postUrl: string;
  title: string;
  /** One-liner shown under the title and in archive/home cards */
  subtitle: string;
  heroImageUrl: string;
  heroImageAlt: string;
  /** 1-2 sentence intro paragraph shown above the five facts */
  intro: string;
  facts: MockFact[];
  publishedAt: string; // ISO-8601
  category: string;
  keywords: string[];
}

export const MOCK_POSTS: MockPost[] = [
  {
    id: "2025-04-30_why-diesel-engines-dont-have-spark-plugs",
    issueNumber: 6,
    slug: "why-diesel-engines-dont-have-spark-plugs",
    postUrl: "/2025/04/30/why-diesel-engines-dont-have-spark-plugs/",
    title: "Why Diesel Engines Don\u2019t Have Spark Plugs",
    subtitle:
      "Diesel ignites fuel through heat alone \u2014 no spark required. Understanding compression ignition reveals why diesel is so remarkably efficient.",
    heroImageUrl:
      "https://images.unsplash.com/photo-1619642751034-765dfdf7c58e?auto=format&fit=crop&w=1600&q=80",
    heroImageAlt: "Close-up of a diesel engine piston and cylinder",
    intro:
      "Open the hood of a diesel truck and you won\u2019t find a distributor, ignition coil, or spark plug wires. That\u2019s not an oversight \u2014 it\u2019s the entire point.",
    publishedAt: "2025-04-30T06:00:00Z",
    category: "Engine Tech",
    keywords: ["diesel", "compression ignition", "engine"],
    facts: [
      {
        title: "Compression does the igniting",
        body: "A gasoline engine compresses a mixture of air and fuel, then fires a spark. A diesel compresses only air \u2014 squeezing it to ratios of 14:1 to 25:1 until the temperature climbs above 500\u00a0\u00b0C. Diesel fuel injected into that superheated air ignites spontaneously.",
        imageUrl:
          "https://images.unsplash.com/photo-1619642751034-765dfdf7c58e?auto=format&fit=crop&w=1200&q=80",
        imageAlt: "Diesel engine cutaway showing piston and valves",
      },
      {
        title: "Rudolf Diesel\u2019s real goal was efficiency",
        body: "When Rudolf Diesel patented his engine in 1892, his stated aim was to eliminate the waste heat losses plaguing steam engines of the era. Removing the electrical ignition system entirely was a means to that efficiency end \u2014 not just a quirk.",
        imageUrl:
          "https://images.unsplash.com/photo-1558618666-fcd25c85cd64?auto=format&fit=crop&w=1200&q=80",
        imageAlt: "Vintage mechanical patent drawings",
      },
      {
        title: "Higher compression extracts more energy",
        body: "The thermodynamic efficiency of any engine rises with compression ratio. Diesel\u2019s high-compression cycle extracts more energy per unit of fuel \u2014 roughly 25\u201340\u00a0% thermal efficiency vs 20\u201330\u00a0% for a naturally aspirated petrol engine. That\u2019s why long-haul trucks are all diesel.",
        imageUrl:
          "https://images.unsplash.com/photo-1601584115197-04ecc0da31d7?auto=format&fit=crop&w=1200&q=80",
        imageAlt: "Semi truck on a highway at dusk",
      },
      {
        title: "Glow plugs are only used at cold start",
        body: "Cold air doesn\u2019t compress to the same temperature as warm air. Glow plugs are small electric heaters that pre-warm the combustion chamber before a cold start. Once the engine is running, they switch off \u2014 they play no part in normal combustion at all.",
        imageUrl:
          "https://images.unsplash.com/photo-1503376780353-7e6692767b70?auto=format&fit=crop&w=1200&q=80",
        imageAlt: "Car engine bay on a frosty morning",
      },
      {
        title: "Diesel fuel itself is less volatile than petrol",
        body: "Drop a match into a puddle of diesel and it won\u2019t ignite \u2014 its flash point is around 52\u00a0\u00b0C, versus \u221243\u00a0\u00b0C for petrol. This lower volatility is what makes compression ignition work: the fuel needs extreme heat from compression, not a casual spark, to combust.",
        imageUrl:
          "https://images.unsplash.com/photo-1487754180451-c456f719a1fc?auto=format&fit=crop&w=1200&q=80",
        imageAlt: "Diesel fuel pump at a petrol station",
      },
    ],
  },
  {
    id: "2025-04-29_why-f1-tires-are-so-wide",
    issueNumber: 5,
    slug: "why-f1-tires-are-so-wide",
    postUrl: "/2025/04/29/why-f1-tires-are-so-wide/",
    title: "The Surprising Reason Formula\u00a01 Cars Have Such Wide Tires",
    subtitle:
      "A Formula\u00a01 rear tire can be 305\u00a0mm wide. That\u2019s not for looks \u2014 it\u2019s pure physics.",
    heroImageUrl:
      "https://images.unsplash.com/photo-1541773367336-d3f9c7f8bc4e?auto=format&fit=crop&w=1600&q=80",
    heroImageAlt: "Formula 1 car cornering at high speed showing wide rear tires",
    intro:
      "Watch any Formula\u00a01 race and the tires are impossible to ignore \u2014 especially the rears. At 305\u00a0mm wide, they\u2019re nearly as broad as a standard kitchen worktop is deep.",
    publishedAt: "2025-04-29T06:00:00Z",
    category: "Motorsport",
    keywords: ["formula 1", "tires", "grip", "downforce"],
    facts: [
      {
        title: "More width = bigger contact patch = more grip",
        body: "The contact patch \u2014 the footprint of rubber actually touching tarmac \u2014 is what generates lateral force. A wider tire creates a larger contact patch, allowing the compound to resist sliding under the enormous sideways loads of high-speed cornering.",
        imageUrl:
          "https://images.unsplash.com/photo-1541773367336-d3f9c7f8bc4e?auto=format&fit=crop&w=1200&q=80",
        imageAlt: "F1 tire contact patch at speed",
      },
      {
        title: "F1 rear tires generate ~2.5 tonnes of lateral force",
        body: "Under maximum cornering load, each rear tire can generate roughly 2.5 tonnes of lateral force. At 300\u00a0km/h, that\u2019s what stops the car from sliding off the road mid-corner. Narrower tires simply cannot produce enough friction to contain that force.",
        imageUrl:
          "https://images.unsplash.com/photo-1606664515524-ed2f786a0bd6?auto=format&fit=crop&w=1200&q=80",
        imageAlt: "F1 car cornering at Monaco Grand Prix",
      },
      {
        title: "Width helps distribute heat evenly",
        body: "Tire compounds only grip within a specific temperature window: too cold and the rubber is hard and slippery; too hot and it degrades rapidly. A wider tire distributes heat across a greater surface area, keeping the compound in its optimal window for longer stints.",
        imageUrl:
          "https://images.unsplash.com/photo-1492144534655-ae79c964c9d7?auto=format&fit=crop&w=1200&q=80",
        imageAlt: "Pirelli tires being heated in blankets in the pit lane",
      },
      {
        title: "Wide tires handle downforce loads",
        body: "Modern F1 cars generate more downforce than their own weight at speed \u2014 a 700+ kg vertical load pressing each axle into the tarmac. Wider tires handle that vertical load without the sidewalls folding, which would cause understeer and unpredictable handling.",
        imageUrl:
          "https://images.unsplash.com/photo-1568605117036-5fe5e7bab0b3?auto=format&fit=crop&w=1200&q=80",
        imageAlt: "F1 car at high speed showing aerodynamic downforce",
      },
      {
        title: "Pirelli supplies around 1,800 sets per race weekend",
        body: "Each driver uses different compounds across practice, qualifying, and race. Pirelli brings hard, medium, soft, and wet compounds to every round \u2014 totalling around 1,800 complete tire sets per Grand Prix weekend. Each set has a lifecycle measured in laps, not miles.",
        imageUrl:
          "https://images.unsplash.com/photo-1525609004556-c46c7d6cf023?auto=format&fit=crop&w=1200&q=80",
        imageAlt: "Stack of Pirelli F1 tires in the paddock",
      },
    ],
  },
  {
    id: "2025-04-28_why-cars-tick-after-engine-off",
    issueNumber: 4,
    slug: "why-cars-tick-after-engine-off",
    postUrl: "/2025/04/28/why-cars-tick-after-engine-off/",
    title: "Why Your Car Makes a Ticking Sound After You Turn It Off",
    subtitle:
      "That rhythmic ticking is completely normal. It\u2019s thermal contraction \u2014 metal cooling down at different rates.",
    heroImageUrl:
      "https://images.unsplash.com/photo-1503376780353-7e6692767b70?auto=format&fit=crop&w=1600&q=80",
    heroImageAlt: "Car parked in driveway at dusk with engine cooling down",
    intro:
      "You\u2019ve just parked. You turn off the engine. A few seconds later \u2014 tick, tick, tick. It slows down, but keeps going for a minute or two.",
    publishedAt: "2025-04-28T06:00:00Z",
    category: "Car Basics",
    keywords: ["cooling", "thermal expansion", "engine sounds"],
    facts: [
      {
        title: "Your exhaust reaches 900\u00a0\u00b0C under hard driving",
        body: "During normal driving, an exhaust system can hit 300\u2013900\u00a0\u00b0C depending on load. The manifold, catalytic converter, and muffler all expand as they heat up \u2014 sometimes by several millimetres. When the engine stops, they begin contracting.",
        imageUrl:
          "https://images.unsplash.com/photo-1503376780353-7e6692767b70?auto=format&fit=crop&w=1200&q=80",
        imageAlt: "Glowing exhaust manifold on a high-performance engine",
      },
      {
        title: "Different metals contract at different rates",
        body: "Exhaust systems mix stainless steel, cast iron, and aluminium \u2014 each with different coefficients of thermal expansion. As these components cool, they slide against each other at slightly different rates, causing the characteristic tick.",
        imageUrl:
          "https://images.unsplash.com/photo-1619642751034-765dfdf7c58e?auto=format&fit=crop&w=1200&q=80",
        imageAlt: "Close-up of exhaust pipe joints and clamps",
      },
      {
        title: "The cadence slows as temperature drops",
        body: "Right after shutdown, the temperature gradient is steepest, so contraction is fastest \u2014 lots of ticks. As the metal approaches ambient temperature, ticks slow and eventually stop. The whole process usually takes 10\u201320 minutes.",
        imageUrl:
          "https://images.unsplash.com/photo-1558618666-fcd25c85cd64?auto=format&fit=crop&w=1200&q=80",
        imageAlt: "Thermometer reading temperature drop over time",
      },
      {
        title: "Steel expands 12\u00a0\u03bcm per metre per \u00b0C",
        body: "A 2-metre exhaust system heating from 20\u00a0\u00b0C to 700\u00a0\u00b0C expands by about 16\u00a0mm \u2014 enough movement to stress joints, which then release that stress as audible clicks during cool-down.",
        imageUrl:
          "https://images.unsplash.com/photo-1487754180451-c456f719a1fc?auto=format&fit=crop&w=1200&q=80",
        imageAlt: "Exhaust pipe with visible expansion joint",
      },
      {
        title: "Running ticks are a different warning",
        body: "Post-shutdown ticking is normal. Ticking while the engine is running is not. A tick at idle that quickens with engine speed typically points to low oil pressure, worn hydraulic lifters, or a failing timing chain tensioner. If you hear it while driving, investigate promptly.",
        imageUrl:
          "https://images.unsplash.com/photo-1601584115197-04ecc0da31d7?auto=format&fit=crop&w=1200&q=80",
        imageAlt: "Mechanic listening to engine with stethoscope",
      },
    ],
  },
  {
    id: "2025-04-27_the-origin-of-the-speedometer",
    issueNumber: 3,
    slug: "the-origin-of-the-speedometer",
    postUrl: "/2025/04/27/the-origin-of-the-speedometer/",
    title: "The Speedometer Was Invented Because Drivers Were Getting Fined",
    subtitle:
      "In 1901, an engineer frustrated by speeding fines designed the first practical automobile speedometer.",
    heroImageUrl:
      "https://images.unsplash.com/photo-1558618666-fcd25c85cd64?auto=format&fit=crop&w=1600&q=80",
    heroImageAlt: "Vintage car speedometer dashboard close-up",
    intro:
      "Before speedometers, drivers had no idea how fast they were going \u2014 and police officers with stopwatches were very good at their jobs.",
    publishedAt: "2025-04-27T06:00:00Z",
    category: "Car History",
    keywords: ["speedometer", "history", "invention"],
    facts: [
      {
        title: "Early speed limits were enforced with stopwatches",
        body: "Police would time cars between measured chalk marks on the road. Speeds as low as 4\u00a0mph were enforced through towns. Drivers had no way to know how fast they were going, making the fines effectively arbitrary.",
        imageUrl:
          "https://images.unsplash.com/photo-1558618666-fcd25c85cd64?auto=format&fit=crop&w=1200&q=80",
        imageAlt: "Vintage police officer with stopwatch",
      },
      {
        title: "Otto Schuler patented the centrifugal speedometer in 1902",
        body: "After receiving one too many fines, engineer Otto Schuler patented a centrifugal-governor speedometer. A shaft connected to the gearbox spun faster as the car moved quicker; weighted arms flung outward via centrifugal force and mechanically moved a needle on a dial.",
        imageUrl:
          "https://images.unsplash.com/photo-1494976388531-d1058494cdd8?auto=format&fit=crop&w=1200&q=80",
        imageAlt: "Early mechanical speedometer mechanism",
      },
      {
        title: "Oldsmobile made it standard in 1910",
        body: "For years, speedometers were expensive accessories for wealthy motorists. Oldsmobile became the first American manufacturer to include one as standard equipment in 1910. The rest of the industry followed rapidly \u2014 within a decade, a car without a speedometer was considered incomplete.",
        imageUrl:
          "https://images.unsplash.com/photo-1525609004556-c46c7d6cf023?auto=format&fit=crop&w=1200&q=80",
        imageAlt: "1910 Oldsmobile dashboard",
      },
      {
        title: "Early speedos were deliberately optimistic",
        body: "Mechanical speedometers of the era were notoriously inaccurate \u2014 often reading 10\u201315\u00a0% high. Manufacturers believed an over-reading was safer than an under-reading. The tradition persists; most modern cars read 3\u20137\u00a0% high by design.",
        imageUrl:
          "https://images.unsplash.com/photo-1503376780353-7e6692767b70?auto=format&fit=crop&w=1200&q=80",
        imageAlt: "Close-up of speedometer needle near the limit",
      },
      {
        title: "GPS has proven just how optimistic they are",
        body: "Smartphone GPS apps allow drivers to compare their speedometer reading against satellite-measured ground speed. Most find their car reads 5\u20137\u00a0% high at typical speeds \u2014 meaning a reading of 100\u00a0km/h usually corresponds to actual travel at about 93\u201395\u00a0km/h.",
        imageUrl:
          "https://images.unsplash.com/photo-1541773367336-d3f9c7f8bc4e?auto=format&fit=crop&w=1200&q=80",
        imageAlt: "Phone GPS navigation showing speed",
      },
    ],
  },
  {
    id: "2025-04-26_what-is-a-jake-brake",
    issueNumber: 2,
    slug: "what-is-a-jake-brake",
    postUrl: "/2025/04/26/what-is-a-jake-brake/",
    title: "What Is a Jake Brake \u2014 and Why Do Trucks Use It?",
    subtitle:
      "That machine-gun rattle from a downhill semi is a compression release brake slowing 40 tonnes without touching the pads.",
    heroImageUrl:
      "https://images.unsplash.com/photo-1601584115197-04ecc0da31d7?auto=format&fit=crop&w=1600&q=80",
    heroImageAlt: "Large semi truck descending a mountain highway",
    intro:
      "You\u2019re driving behind a truck on a mountain road and it suddenly makes a sound like a giant machine gun. No, the engine isn\u2019t failing. That\u2019s a compression release brake.",
    publishedAt: "2025-04-26T06:00:00Z",
    category: "Trucks & Commercial",
    keywords: ["jake brake", "engine braking", "trucks", "compression release"],
    facts: [
      {
        title: "A loaded semi weighs up to 40 tonnes",
        body: "A fully loaded 18-wheeler can tip the scales at 36\u201340 tonnes. On a 10-mile mountain descent, conventional disc brakes alone would overheat and \u2018fade\u2019 within a few miles. Brake fade at that weight is catastrophic \u2014 a truck needs supplemental braking.",
        imageUrl:
          "https://images.unsplash.com/photo-1601584115197-04ecc0da31d7?auto=format&fit=crop&w=1200&q=80",
        imageAlt: "Loaded semi truck on a steep downgrade",
      },
      {
        title: "It works by releasing compressed air",
        body: "In normal diesel operation: compress air \u2192 inject fuel \u2192 combustion \u2192 power. A Jake Brake adds a step: at the top of the compression stroke, it opens the exhaust valves and dumps the compressed air. The engine did work compressing it and gets none back as power \u2014 strong engine braking results.",
        imageUrl:
          "https://images.unsplash.com/photo-1619642751034-765dfdf7c58e?auto=format&fit=crop&w=1200&q=80",
        imageAlt: "Diesel engine valvetrain cutaway",
      },
      {
        title: "It can generate 600\u00a0hp of retarding force",
        body: "On a large diesel engine, compression release braking can generate up to 600\u00a0hp of retarding force \u2014 comparable to the engine\u2019s rated output. This allows a driver to descend a long grade with minimal use of friction brakes, keeping them cool and ready for emergency stops.",
        imageUrl:
          "https://images.unsplash.com/photo-1568605117036-5fe5e7bab0b3?auto=format&fit=crop&w=1200&q=80",
        imageAlt: "Truck descending steep mountain pass",
      },
      {
        title: "The Jacobs Engine Brake was patented in 1961",
        body: "The compression release brake was commercialised by the Jacobs Manufacturing Company \u2014 hence \u2018Jake Brake\u2019. Their first patent was filed in 1961. Jacobs remains the dominant supplier; the trade name became the generic term, like Hoover for vacuum cleaners.",
        imageUrl:
          "https://images.unsplash.com/photo-1492144534655-ae79c964c9d7?auto=format&fit=crop&w=1200&q=80",
        imageAlt: "Jacobs Engine Brake unit on a diesel engine",
      },
      {
        title: "Many towns ban Jake brakes on local roads",
        body: "Compression release braking releases compressed air pulses through the exhaust \u2014 once per cylinder per cycle \u2014 creating a loud, rapid hammering sound audible from hundreds of metres away. Residential areas near highways frequently post signs reading \u2018Engine Brake Prohibited\u2019.",
        imageUrl:
          "https://images.unsplash.com/photo-1503376780353-7e6692767b70?auto=format&fit=crop&w=1200&q=80",
        imageAlt: "Road sign prohibiting engine brakes near residential area",
      },
    ],
  },
  {
    id: "2025-04-25_why-engine-oil-turns-black",
    issueNumber: 1,
    slug: "why-engine-oil-turns-black",
    postUrl: "/2025/04/25/why-engine-oil-turns-black/",
    title: "Why Does Your Engine Oil Turn Black?",
    subtitle:
      "Fresh oil is amber. Used oil is black. The colour change isn\u2019t dirt \u2014 it\u2019s the oil doing its job.",
    heroImageUrl:
      "https://images.unsplash.com/photo-1487754180451-c456f719a1fc?auto=format&fit=crop&w=1600&q=80",
    heroImageAlt: "Mechanic checking engine oil dipstick",
    intro:
      "Pull your dipstick after 2,000 miles on fresh oil and it might already be dark brown or black. Is something wrong? Almost certainly not.",
    publishedAt: "2025-04-25T06:00:00Z",
    category: "Car Maintenance",
    keywords: ["engine oil", "oil change", "maintenance"],
    facts: [
      {
        title: "Combustion byproducts sneak past the piston rings",
        body: "No combustion is perfect. Small amounts of gases \u2014 containing soot, unburned fuel, and acidic compounds \u2014 pass the piston rings and enter the crankcase. Without protection, these would deposit as varnish and sludge on the valve train and oil passages.",
        imageUrl:
          "https://images.unsplash.com/photo-1487754180451-c456f719a1fc?auto=format&fit=crop&w=1200&q=80",
        imageAlt: "Engine piston ring on white background",
      },
      {
        title: "Detergent additives capture the soot",
        body: "Modern engine oil contains detergent and dispersant additives engineered to chemically trap combustion byproducts and hold them in suspension. The black colour you see on a dipstick is soot suspended in oil \u2014 the additives are doing exactly what they were designed to do.",
        imageUrl:
          "https://images.unsplash.com/photo-1558618666-fcd25c85cd64?auto=format&fit=crop&w=1200&q=80",
        imageAlt: "Clear vs black oil comparison on a white surface",
      },
      {
        title: "Dark oil is working oil",
        body: "Paradoxically, very dark oil is often healthier than pale oil that stays light too long. If the soot weren\u2019t suspended in the oil, it would precipitate as hard carbon deposits on your camshaft lobes, valve stems, and oil galleries \u2014 the deposits that cause real engine wear.",
        imageUrl:
          "https://images.unsplash.com/photo-1619642751034-765dfdf7c58e?auto=format&fit=crop&w=1200&q=80",
        imageAlt: "Engine oil passages showing clean internals",
      },
      {
        title: "Colour is a poor indicator of when to change",
        body: "The real question isn\u2019t colour \u2014 it\u2019s whether the additive package is depleted. A Total Base Number (TBN) test measures the remaining alkaline reserve for neutralising acids. When TBN approaches zero, it\u2019s time to change regardless of colour.",
        imageUrl:
          "https://images.unsplash.com/photo-1601584115197-04ecc0da31d7?auto=format&fit=crop&w=1200&q=80",
        imageAlt: "Oil analysis test kit",
      },
      {
        title: "Synthetic oil resists breakdown far longer",
        body: "Full synthetic oils have more uniform molecular structures, making them inherently more stable under heat and shear stress. A quality full synthetic can hold its detergent reserve for 10,000\u201315,000 miles \u2014 three to four times a conventional oil\u2019s service life.",
        imageUrl:
          "https://images.unsplash.com/photo-1503376780353-7e6692767b70?auto=format&fit=crop&w=1200&q=80",
        imageAlt: "Synthetic oil bottle being poured into engine",
      },
    ],
  },
];

export function getMockPost(slug: string): MockPost | undefined {
  return MOCK_POSTS.find((p) => p.slug === slug);
}

export function getMockPostByParams(
  _year: string,
  _month: string,
  _day: string,
  slug: string
): MockPost | undefined {
  return MOCK_POSTS.find((p) => p.slug === slug);
}

export function getLatestPost(): MockPost {
  return MOCK_POSTS[0];
}

export function getPastPosts(): MockPost[] {
  return MOCK_POSTS.slice(1);
}
