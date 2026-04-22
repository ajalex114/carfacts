# CAR FACTS DAILY — SOCIAL MEDIA PLAN

**Goal:** Grow traffic + brand visibility using free platforms only  
**Method:** One content engine → distributed everywhere  
**Focus:** Do ONE platform at a time, in order

---

## 🔧 GROUND RULES (Apply to ALL Platforms)

All new social media integrations must follow the same pattern established by the existing Twitter implementation.

### 1. Feature Toggle System
- Every social media platform has a **master enable/disable** toggle (e.g. `SocialMedia:TwitterEnabled`).
- Every **sub-feature** within a platform has its own **independent enable/disable** toggle (e.g. `SocialMedia:LikesEnabled`, `SocialMedia:RepliesEnabled`).
- A disabled platform disables all its sub-features automatically.
- Sub-features can be individually disabled even when the platform is enabled.

### 2. Cadence Configuration (Follow Twitter Pattern)
- Every sub-feature accepts a **min–max range** for daily cadence, just like Twitter does today:
  - `LikesPerDayMin` / `LikesPerDayMax` (Twitter default: 10–20)
  - `RepliesPerDayMin` / `RepliesPerDayMax` (Twitter default: 3–6)
  - `FactsPerDay` (Twitter default: 5)
  - `LinkPostsPerDay` (Twitter default: 1)
- A random count within the range is picked each day — same approach as Twitter.
- Cadence is configured **per sub-feature, per platform** in app settings.

### 3. Scheduling — Randomness is Mandatory (Follow Twitter Pattern)
Posts must **never appear at the same times on consecutive days**. Every platform must use the same randomization strategy Twitter uses today via `UsPostingScheduler`:

- **Daily randomization:** A day-seeded RNG generates a unique schedule each day — no two days look alike.
- **Time windows:** Posts are distributed across 4 US-friendly windows (round-robin):
  - US Morning (7–10 AM ET / UTC 11–14)
  - US Lunch (12–2 PM ET / UTC 16–18)
  - US Evening (5–7 PM ET / UTC 21–23)
  - US Dinner (8–10 PM ET / UTC 00–02 next day)
- **Jitter:** Every post gets ±15 min random offset so times shift day to day.
- **Minimum gap:** At least 20 min between any two posts.
- **Replies:** Interspersed in gaps between existing posts (≥30 min gap, non-consecutive placement, ±5 min jitter).
- **Likes:** Clubbed in random groups of 2–3, fired 30 sec apart, with ≥15 min between groups. Groups are distributed across windows with ±10 min jitter.
- **Per-platform cron:** Each platform can define its own trigger cron expression (e.g. Pinterest's `PinterestPostingCronExpression`), but the actual post times within that day are always randomized.

> **Key principle:** The cron trigger decides *which days* to post. The `UsPostingScheduler` decides *what times* on that day — and those times are always random.

### 4. Secrets Management
- All API keys, tokens, and secrets are stored in **Azure Key Vault** (class: `SecretNames`).
- No secrets in app configuration, environment variables, or source code.
- Each platform's credentials are stored as separate Key Vault entries.
- Existing pattern: `Twitter-ConsumerKey`, `Twitter-ConsumerSecret`, `Twitter-AccessToken`, `Twitter-AccessTokenSecret`.

---

## ✅ PHASE 0 — Web Stories (Already Built)
Web Stories are the **content engine** for all other platforms. Already fully implemented.

| Sub-Feature          | Toggle Setting                   | Cadence Setting                              | Default         |
|----------------------|----------------------------------|----------------------------------------------|-----------------|
| Generate Stories     | `WebStories:Enabled`             | Runs with daily blog post pipeline           | ✅ On           |
| Publish to WordPress | (part of blog orchestrator)      | 1 story per blog post                        | ✅ On           |

**What it produces (reusable across all platforms):**
- 720×1280 vertical images (per fact) — perfect 9:16 ratio
- Short fact text (≤280 chars) with catchy title
- AI-generated images with text overlays
- Car model + year metadata
- Cover page + CTA page with blog post link

**Content reuse map:**
| Web Story Asset            | Instagram          | Facebook           | Pinterest          | YouTube            |
|----------------------------|--------------------|--------------------|--------------------|--------------------|
| Vertical image (720×1280)  | Reel/Story visual  | Post image/Reel    | Pin image          | Short thumbnail    |
| Catchy title               | Reel hook text     | Post headline      | Pin title          | Short title        |
| Fact text (≤280 chars)     | Caption            | Post text          | Pin description    | Short description  |
| Car model + year           | Hashtags/caption   | Hashtags           | SEO keywords       | Hashtags           |
| Blog post URL              | Bio link / CTA     | Link post          | Pin link           | Description link   |

> All platforms draw from the same content pool — no duplicate content generation needed.

**Secrets (Key Vault):** None additional (uses existing WordPress + AI secrets).

---

## ✅ PHASE 1 — Pinterest (Least Work — Already 80% Built)
Pinterest is a visual search engine — perfect for car facts. Most of the code already exists (service, orchestrator, trigger, board taxonomy, tracking).

| Sub-Feature        | Toggle Setting                  | Cadence Setting (Min–Max)                    | Default         |
|--------------------|---------------------------------|----------------------------------------------|-----------------|
| Create Pin         | `Pinterest:PostsEnabled`        | `Pinterest:PinsPerDay` (fixed)               | 6/day           |
| Post Link Pin      | `Pinterest:LinkPostsEnabled`    | `Pinterest:LinkPinsPerDay` (fixed)           | 1/day           |
| Reply to Comments  | `Pinterest:RepliesEnabled`      | `Pinterest:RepliesPerDayMin/Max`             | 0–5/day (off)   |

**Content source:** Reuses web story images + fact text. Links pins to blog articles.

**Why this is first:**
- ~80% of code already exists in codebase
- Pins rank for months or years (long-term SEO traffic)
- Simple API — create dev app, get token, done
- **Free**, no cost

**Remaining work:** Wire up sub-feature toggles + cadence ranges to existing orchestrator.

**Secrets (Key Vault):**
- `Pinterest-AccessToken` (already exists)
- `pinterest-app-id`
- `pinterest-app-secret`

---

## ✅ PHASE 2 — Facebook Page (Service Exists, Shares Meta App)
Facebook still gives strong organic reach for car content. Service code already partially built.

| Sub-Feature             | Toggle Setting                   | Cadence Setting (Min–Max)                     | Default         |
|-------------------------|----------------------------------|-----------------------------------------------|-----------------|
| Post Image/Reel         | `Facebook:PostsEnabled`          | `Facebook:FactsPerDay` (fixed)                | 5/day           |
| Post Link               | `Facebook:LinkPostsEnabled`      | `Facebook:LinkPostsPerDay` (fixed)            | 1/day           |
| Auto-share from Insta   | `Facebook:AutoShareEnabled`      | Mirrors Instagram cadence                     | —               |
| Reply to Comments       | `Facebook:RepliesEnabled`        | `Facebook:RepliesPerDayMin/Max`               | 3–6/day         |
| Like Comments           | `Facebook:LikesEnabled`          | `Facebook:LikesPerDayMin/Max`                 | 10–20/day       |
| Share to Groups         | `Facebook:GroupShareEnabled`     | `Facebook:GroupSharesPerDayMin/Max`           | 0–3/day (off)   |

**Content source:** Reuses web story images + fact text.

**Why this is second:**
- FacebookService already exists in codebase
- Shares Meta app with Instagram (set up once, use for both)
- Reels perform well on Facebook too
- **Free**, no cost

**Remaining work:** Build orchestrator, trigger, and wire toggles/cadence.

**Secrets (Key Vault):**
- `Facebook-PageAccessToken` (already exists)
- `facebook-page-id`
- (shares `meta-app-id` and `meta-app-secret` with Instagram)

---

## ✅ PHASE 3 — Instagram (Reuses Web Story Content, but Meta App Review Required)
Instagram reuses the exact same vertical images and fact text from web stories — zero extra content generation.

| Sub-Feature        | Toggle Setting                  | Cadence Setting (Min–Max)                    | Default         |
|--------------------|---------------------------------|----------------------------------------------|-----------------|
| Post Reel          | `Instagram:PostsEnabled`        | `Instagram:FactsPerDay` (fixed)              | 5/day           |
| Post Link          | `Instagram:LinkPostsEnabled`    | `Instagram:LinkPostsPerDay` (fixed)          | 1/day           |
| Reply to Comments  | `Instagram:RepliesEnabled`      | `Instagram:RepliesPerDayMin/Max`             | 3–6/day         |
| Like Comments      | `Instagram:LikesEnabled`        | `Instagram:LikesPerDayMin/Max`               | 10–20/day       |
| Post Story         | `Instagram:StoriesEnabled`      | `Instagram:StoriesPerDay` (fixed)            | 0/day (off)     |

**Content source:** Reuses web story images (720×1280) + fact text as captions. Just needs a formatter, not a generator.

**Why this is third (not first):**
- Content is free (web story reuse), coding effort is moderate
- **But:** Meta App Review for `instagram_content_publish` permission can take weeks
- Bundle the Meta app setup with Facebook (Phase 2) to avoid double work
- **Free**, no cost

**⚠️ Blocker:** Start Meta App Review during Phase 2 so it's approved by the time you reach Phase 3.

**Secrets (Key Vault):**
- `instagram-access-token`
- (shares `meta-app-id` and `meta-app-secret` with Facebook)

---

## ✅ PHASE 4 — YouTube Shorts (New Integration — Most Work)
Entirely new platform integration. Uses Google APIs + OAuth2.

| Sub-Feature          | Toggle Setting                | Cadence Setting (Min–Max)                  | Default         |
|----------------------|-------------------------------|--------------------------------------------|-----------------|
| Upload Short         | `YouTube:PostsEnabled`        | `YouTube:FactsPerDay` (fixed)              | 5/day           |
| Post Link            | `YouTube:LinkPostsEnabled`    | `YouTube:LinkPostsPerDay` (fixed)          | 1/day           |
| Reply to Comments    | `YouTube:RepliesEnabled`      | `YouTube:RepliesPerDayMin/Max`             | 3–6/day         |
| Like Comments        | `YouTube:LikesEnabled`        | `YouTube:LikesPerDayMin/Max`               | 10–20/day       |

**Content source:** Reuses web story images + fact text. Needs video assembly (image → short video with text overlay).

**Why this is last:**
- Completely new integration (no existing code)
- OAuth2 consent screen + scope configuration is complex
- May need video generation tooling (images → video)
- **Free** (10K API units/day ≈ 6 uploads), but most coding effort

**Secrets (Key Vault):**
- `youtube-api-key`
- `youtube-oauth-client-id`
- `youtube-oauth-client-secret`
- `youtube-refresh-token`

---

## ⏸️ PHASE 5 — LinkedIn (Parked — Not in use)
LinkedIn is powerful for EV/tech/industry content but generic car facts don't fit the professional audience well. Parked until we have a clear content strategy for this platform.

| Sub-Feature              | Toggle Setting                  | Cadence Setting (Min–Max)                    | Default         |
|--------------------------|---------------------------------|----------------------------------------------|-----------------|
| Post (Short/Carousel)    | `LinkedIn:PostsEnabled`         | `LinkedIn:PostsPerWeek` (fixed)              | 3/week          |
| Post Link                | `LinkedIn:LinkPostsEnabled`     | `LinkedIn:LinkPostsPerWeek` (fixed)          | 1/week          |
| Reply to Comments        | `LinkedIn:RepliesEnabled`       | `LinkedIn:RepliesPerDayMin/Max`              | 0–5/day (off)   |
| Like Comments            | `LinkedIn:LikesEnabled`         | `LinkedIn:LikesPerDayMin/Max`                | 0–10/day (off)  |

**If we revisit LinkedIn, content should be filtered to:**
- EV industry trends
- Automotive tech / autonomous driving
- Sustainability / green transport
- Data-driven car insights (cost analysis, market data)

> ⚠️ Generic car trivia does NOT work on LinkedIn. Only professional/industry-angle content.

**Secrets (Key Vault):**
- `linkedin-access-token`
- `linkedin-client-id`
- `linkedin-client-secret`

---

## ⭐ CONFIGURATION SUMMARY

### Platform Master Toggles
| Platform     | Default Enabled | Phase |
|--------------|-----------------|-------|
| Web Stories  | ✅ Yes          | 0 (already built) |
| Pinterest    | ✅ Yes          | 1     |
| Facebook     | ❌ No           | 2     |
| Instagram    | ❌ No           | 3     |
| YouTube      | ❌ No           | 4     |
| LinkedIn     | ❌ No           | ⏸️ Parked |

> Enable platforms one at a time as you progress through phases.

### Key Vault Entries (All Platforms)
| Key Vault Entry                | Platform   |
|--------------------------------|------------|
| `meta-app-id`                  | Instagram, Facebook |
| `meta-app-secret`              | Instagram, Facebook |
| `instagram-access-token`       | Instagram  |
| `facebook-page-access-token`   | Facebook   |
| `facebook-page-id`             | Facebook   |
| `youtube-api-key`              | YouTube    |
| `youtube-oauth-client-id`      | YouTube    |
| `youtube-oauth-client-secret`  | YouTube    |
| `youtube-refresh-token`        | YouTube    |
| `pinterest-access-token`       | Pinterest  |
| `pinterest-app-id`             | Pinterest  |
| `pinterest-app-secret`         | Pinterest  |
| `linkedin-access-token`        | LinkedIn   |
| `linkedin-client-id`           | LinkedIn   |
| `linkedin-client-secret`       | LinkedIn   |

---

## ⭐ YOUR WEEKLY WORKFLOW (Simple + Repeatable)

**Day 1 — Create**
- Write 7 car facts
- Create 7 reels
- Create 7 pins

**Day 2 — Schedule**
- Instagram → Meta Business Suite
- Facebook → Meta Business Suite
- YouTube → YouTube Studio
- Pinterest → Pinterest Scheduler
- LinkedIn → Built-in scheduler

**Day 3–7 — Engage**
- Reply to comments (per sub-feature cadence)
- Share to Facebook groups (if enabled)
- Answer YouTube comments (if enabled)

---

## ⭐ PHASE ORDER (Follow this exactly — ordered by least effort + zero cost)

1. **Web Stories** → ✅ Already built and running (Phase 0)
2. **Pinterest** → ~80% code exists, simple API, free → enable master toggle
3. **Facebook** → service exists, shares Meta app, free → enable master toggle
4. **Instagram** → reuses web story content, free, but start Meta App Review during Phase 2 → enable master toggle
5. **YouTube** → new integration, free tier, most coding work → enable master toggle
6. **LinkedIn** → ⏸️ Parked (content doesn't fit audience)

> ⚠️ Start Meta App Review during Phase 2 (Facebook) so Instagram is unblocked by Phase 3.
> You master one platform at a time.
> No stress. No chaos. No multitasking.
