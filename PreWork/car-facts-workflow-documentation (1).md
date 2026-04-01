# Daily Car Facts to WordPress - n8n Workflow Project

## Project Overview

**Goal:** Automated daily blog posts featuring 5 historical car facts with AI-generated content, images, catchy titles, and full SEO/GEO optimization.

**Platform:** Self-hosted n8n workflow automation

**Target:** WordPress blog with daily automotive history content

---

## Workflow Summary

### What It Does

1. **Runs daily at 6 AM** (configurable via cron)
2. **Generates 5 unique car facts** that happened "on this day in history" (different years, different decades)
3. **Creates catchy, clickbait titles** for main post and each individual fact
4. **Generates SEO metadata** (meta description, keywords)
5. **Creates GEO optimization** for AI search engines (ChatGPT, Perplexity, Claude)
6. **Generates 5 professional images** (1024×1024) using AI
7. **Publishes to WordPress** with full schema markup and structured data

---

## Tech Stack

### AI Services

**Text Generation:**
- **Provider:** Azure OpenAI
- **Model:** GPT-4o-mini
- **Temperature:** 0.85 (balances accuracy and creativity)
- **Usage:** Single AI call generates facts + titles + SEO + GEO data

**Image Generation:**
- **Provider:** Stability AI
- **Model:** Stable Diffusion XL 1024
- **Usage:** 5 images per day (one per fact)

### Platform

- **Workflow Engine:** Self-hosted n8n
- **Publishing Target:** WordPress (via REST API)
- **Authentication:** WordPress Application Passwords

---

## Cost Analysis

### Monthly Costs (INR)

**AI Services:**
- Text Generation (Azure GPT-4o-mini): ₹12-15/month
- Image Generation (Stability AI SDXL): ₹37-40/month
- **Total AI Costs:** ₹49-55/month

**Infrastructure:**
- Self-hosted n8n: ₹300-500/month (VPS/server)
- **Grand Total:** ₹349-555/month

### Cost Optimization Achieved

- **Original approach:** 2 AI calls per workflow (₹62-70/month)
- **Optimized approach:** 1 AI call per workflow (₹49-55/month)
- **Savings:** ₹13-15/month by combining fact generation with SEO/title generation

---

## Workflow Architecture

### Flow Diagram

```
[Schedule Trigger] (Daily 6 AM)
    ↓
[Get Current Date] (Format: "March 12")
    ↓
[Build Content Prompt] (Comprehensive prompt with all requirements)
    ↓
[AI Agent - Generate Everything] ← [Azure OpenAI Chat Model]
    ↓
[Parse & Validate Response] (Extract facts + SEO data)
    ↓
[Stability AI - Generate Image] (Loop: 5 times, one per fact)
    ↓
[Process Image Data] (Convert base64 to binary)
    ↓
[Upload Images to WordPress] (via WP REST API)
    ↓
[Aggregate All Data] (Combine 5 facts + images)
    ↓
[Format WordPress Content] (Build SEO/GEO optimized HTML)
    ↓
[Create WordPress Post] (Publish with Yoast meta fields)
    ↓
[Log Success]
```

### Key Nodes

1. **Schedule Trigger** - Cron: `0 6 * * *` (6 AM daily)
2. **Get Current Date** - Extracts date in "MMMM d" format (e.g., "March 12")
3. **Build Content Prompt** - Creates comprehensive prompt for AI
4. **AI Agent - Generate Everything** - Single call generates all content
5. **Parse & Validate Response** - Validates JSON structure and extracts data
6. **Stability AI - Generate Image** - Creates images (loops 5 times)
7. **Process Image Data** - Converts to WordPress-compatible format
8. **Upload Images to WordPress** - Uploads to WP media library
9. **Aggregate All Data** - Combines facts with uploaded images
10. **Format WordPress Content** - Builds SEO/GEO optimized HTML
11. **Create WordPress Post** - Publishes via REST API
12. **Log Success** - Records execution details

---

## AI Prompt Strategy

### Comprehensive Single-Call Prompt

The workflow uses ONE AI call that generates:
- 5 historical automotive facts (4-6 sentences each)
- Catchy, clickbait main title (60-70 chars)
- Individual catchy titles for each fact (40-60 chars)
- SEO meta description (150-160 chars)
- 5-7 relevant keywords
- GEO summary for AI search engines

### Exact System Prompt

**Node:** AI Agent - Generate Everything

**System Message:**
```
You are a dual-expert AI combining automotive history knowledge with SEO/copywriting expertise. You create accurate, detailed automotive facts while simultaneously crafting catchy, clickbait titles and SEO-optimized metadata. You understand both traditional search engines (Google) and AI search engines (ChatGPT, Perplexity, Claude). You ALWAYS return valid JSON only with no additional text, markdown formatting, or code blocks.
```

### Exact User Prompt

**Node:** Build Content Prompt

**Prompt Variable:** `content_prompt`

**Full Prompt Text:**
```
Generate exactly 5 unique and fascinating automotive facts that happened on {{ $('Get Current Date').item.json.today_date }} throughout history, PLUS create SEO-optimized titles and metadata.

📋 CONTENT REQUIREMENTS:
- Each fact must be from a DIFFERENT year and DIFFERENT decade
- Include facts from various eras (1920s through 2020s)
- Make facts engaging, detailed, and historically accurate
- Each fact should be 4-6 sentences long with rich context
- Focus on significant events: car launches, racing victories, industry milestones, innovations
- Explain WHY this fact mattered and its impact on the automotive industry
- Include specific details: locations, people involved, technical specifications when relevant

🎯 SEO & TITLE REQUIREMENTS:
- Create a CATCHY, CLICKBAIT main page title (60-70 characters)
- Create individual CATCHY titles for each fact (40-60 characters each)
- Use power words: "Shocking", "Incredible", "Revolutionary", "Secret", "Never Knew", "Untold"
- Create curiosity gaps: "You Won't Believe...", "The Hidden Truth...", "What Happened When..."
- Include numbers and dates for credibility
- Make titles emotional and engaging
- Generate SEO meta description (150-160 characters)
- Generate 5-7 relevant SEO keywords
- Generate a GEO summary (2-3 sentences optimized for AI search engines like ChatGPT, Perplexity, Claude)

Return ONLY valid JSON with this exact structure (no markdown, no code blocks, just pure JSON):
{
  "main_title": "Catchy clickbait main page title that makes people want to click",
  "meta_description": "SEO-optimized meta description 150-160 characters",
  "keywords": ["keyword1", "keyword2", "keyword3", "keyword4", "keyword5"],
  "geo_summary": "2-3 sentence summary optimized for AI search engines, focusing on key facts and historical significance",
  "facts": [
    {
      "year": 1955,
      "catchy_title": "Catchy engaging title for this specific fact",
      "fact": "On this day in 1955, detailed fascinating fact with rich context and impact analysis",
      "car_model": "Specific car name or brand involved",
      "image_prompt": "Detailed visual description: a photorealistic image of [specific car model], [key visual details like color, angle, setting], [historical context], professional automotive photography style, high quality, detailed, 8k"
    }
  ]
}

Make sure the image_prompt is detailed and specific enough for accurate image generation. Include visual details like era-appropriate styling, colors, settings, and atmosphere.
```

**Note:** The `{{ $('Get Current Date').item.json.today_date }}` is an n8n expression that inserts the current date in "MMMM d" format (e.g., "March 12")

### JSON Output Structure

```json
{
  "main_title": "Catchy clickbait main page title",
  "meta_description": "SEO-optimized meta description 150-160 characters",
  "keywords": ["keyword1", "keyword2", "keyword3", "keyword4", "keyword5"],
  "geo_summary": "2-3 sentence summary optimized for AI search engines",
  "facts": [
    {
      "year": 1955,
      "catchy_title": "Catchy engaging title for this specific fact",
      "fact": "Detailed fascinating fact with rich context and impact",
      "car_model": "Specific car name or brand involved",
      "image_prompt": "Detailed visual description for image generation"
    }
  ]
}
```

---

## SEO Strategy

### Date-Specific SEO Approach

**Decision:** Include DATE (month + day) but NOT YEAR

**Reasoning:**
- ✅ Targets date-specific searches: "March 12 car history"
- ✅ Creates clear daily series: one post per calendar date
- ✅ No content cannibalization: each day is unique
- ✅ Evergreen content: works every March 12 forever
- ✅ Builds authority over time

**Title Format:**
```
Good: "5 Shocking Car Moments from March 12 in Automotive History"
Bad: "5 Car Facts from March 12, 2026" (includes year - becomes dated)
```

**URL Format:**
```
Good: /automotive-history-march-12/
Bad: /car-facts-march-12-2026/ (includes year)
```

### SEO Features Implemented

1. **Schema.org Structured Data:**
   - Article schema
   - NewsArticle schema for each fact
   - ImageObject schema
   - FAQPage schema

2. **Meta Tags:**
   - Optimized meta description
   - Keywords (via Yoast SEO integration)
   - Open Graph tags (WordPress default)

3. **Content Structure:**
   - Table of contents (improves dwell time)
   - Proper heading hierarchy (H2, H3)
   - FAQ section
   - "Why This Matters" sections

4. **Internal Linking:**
   - Next/previous day suggestions
   - Related topic clusters

---

## GEO (Generative Engine Optimization)

### Optimization for AI Search Engines

**Target Engines:**
- ChatGPT
- Perplexity
- Claude
- Google Gemini
- Microsoft Copilot

### GEO Features

1. **GEO Summary Comment:**
   ```html
   <!-- GEO Summary: This article explores 5 groundbreaking automotive 
   moments from March 12 throughout history... -->
   ```

2. **Structured Context:**
   - Clear explanations of significance
   - "Why This Matters" sections
   - Historical impact analysis

3. **Key Takeaways Section:**
   - Helps AI extract main points
   - Summary of all 5 facts

4. **Natural Language:**
   - Written to answer common AI queries
   - Conversational but informative

---

## Content Requirements

### Facts Generation

**Per Fact:**
- 4-6 sentences long
- Rich context and historical detail
- Explain WHY it mattered
- Include specific details: locations, people, specs
- From different year AND different decade
- Cover various eras (1920s through 2020s)

**Topics:**
- Car launches
- Racing victories
- Industry milestones
- Technological innovations
- Design breakthroughs

### Title Generation

**Main Title Guidelines:**
- 60-70 characters
- Catchy, clickbait style
- Use power words: "Shocking", "Incredible", "Revolutionary", "Secret"
- Create curiosity gaps: "You Won't Believe...", "The Hidden Truth..."
- Include numbers for credibility
- Make it emotional and engaging

**Individual Fact Titles:**
- 40-60 characters each
- Same power word strategy
- Specific to each fact
- Avoid generic "1. 1955: Car Model" format

**Examples:**
```
Main Title:
"Mind-Blowing: 5 Car Legends from March 12 That Rewrote History"

Fact Titles:
"🏎️ The $70M Secret Ferrari Didn't Want You to Know"
"Shocking Discovery: The 1977 Turbo That Destroyed All Records"
"You Won't Believe What Happened When Porsche Unveiled the 911"
```

### Image Generation

**Per Image:**
- 1024×1024 resolution
- Photorealistic style
- Professional automotive photography
- Era-appropriate styling
- Detailed prompts including: car model, color, angle, setting, context

---

## WordPress Integration

### REST API Endpoints Used

1. **Media Upload:**
   ```
   POST /wp-json/wp/v2/media
   ```
   - Uploads images
   - Sets title, alt text, caption
   - Returns media ID and URL

2. **Post Creation:**
   ```
   POST /wp-json/wp/v2/posts
   ```
   - Creates post with title, content, excerpt
   - Sets featured media
   - Sets post status (publish/draft)
   - Adds Yoast SEO meta fields

### Authentication

- **Method:** WordPress Application Passwords
- **Setup:** WordPress Admin → Users → Application Passwords
- **Format:** Username + Application Password (Basic Auth)

### Post Structure

```html
<!-- GEO Summary Comment -->
<div class="car-facts-intro" itemscope itemtype="https://schema.org/Article">
  <!-- Schema markup -->
  <p>Introduction paragraph</p>
</div>

<!-- Table of Contents -->
<div class="table-of-contents">
  <h2>📋 Quick Navigation</h2>
  <ol>
    <li><a href="#fact-1">Catchy Fact Title 1</a></li>
    ...
  </ol>
</div>

<!-- Each Fact Section -->
<div class="car-fact-section" id="fact-1" itemscope itemtype="https://schema.org/NewsArticle">
  <h2>🏆 [Catchy AI-Generated Title]</h2>
  <p class="fact-year">Year: 1955 | Vehicle: Ferrari 250 GTO</p>
  <figure itemprop="image">
    <img src="..." alt="..." />
    <figcaption>...</figcaption>
  </figure>
  <div itemprop="articleBody">
    <p>[Fact content]</p>
  </div>
  <div class="impact-section">
    <p><strong>💡 Why This Matters:</strong> ...</p>
  </div>
</div>

<!-- Key Takeaways -->
<div class="car-facts-conclusion">
  <h3>🎯 Key Takeaways</h3>
  <p>Summary paragraph</p>
</div>

<!-- FAQ Section -->
<div class="faq-section" itemscope itemtype="https://schema.org/FAQPage">
  ...
</div>
```

---

## Configuration Requirements

### Placeholders to Replace

1. **Stability AI:**
   - Node: "Stability AI - Generate Image"
   - Replace: `YOUR_STABILITY_AI_API_KEY`
   - Get from: https://platform.stability.ai/

2. **Azure OpenAI:**
   - Node: "Azure OpenAI Chat Model"
   - Configure via n8n credentials manager
   - Need: Resource name, Deployment name, API key

3. **WordPress:**
   - Nodes: "Upload Images to WordPress", "Create WordPress Post"
   - Replace: `YOUR_WORDPRESS_SITE.com`
   - Configure WordPress API credentials
   - Need: Site URL, Username, Application Password

### Optional Configurations

1. **Schedule:**
   - Node: "Schedule Trigger"
   - Current: `0 6 * * *` (6 AM daily)
   - Modify cron expression for different times

2. **Post Status:**
   - Node: "Format WordPress Content"
   - Change `"status": "publish"` to `"status": "draft"` to review before publishing

3. **Categories/Tags:**
   - Node: "Create WordPress Post"
   - Add WordPress category/tag IDs in the JSON body

4. **Temperature:**
   - Node: "Azure OpenAI Chat Model"
   - Current: 0.85
   - Adjust for more/less creativity

---

## Workflow Features

### Implemented Features

✅ Daily scheduled execution
✅ Historical date-based content generation
✅ AI-generated catchy titles (main + individual facts)
✅ SEO optimization (meta, keywords, schema markup)
✅ GEO optimization (AI search engine friendly)
✅ Professional image generation (5 per post)
✅ WordPress auto-publishing
✅ Table of contents
✅ FAQ section with schema
✅ Structured data markup
✅ Image alt text optimization
✅ Yoast SEO integration
✅ Error handling and validation
✅ Success logging

### Not Implemented (Future Enhancements)

- Duplicate fact prevention (database tracking)
- Social media auto-posting
- Email notifications on publish
- Analytics tracking
- Automatic category assignment
- Tag generation from keywords
- Multiple language support
- Image watermarking
- Backlink tracking

---

## Design Decisions

### Key Architectural Choices

1. **Single AI Call vs Two Calls:**
   - **Decision:** Single call for facts + SEO + titles
   - **Reasoning:** Reduces cost, faster execution, simpler architecture
   - **Savings:** ₹13-15/month

2. **AI Agent vs Direct HTTP:**
   - **For Text:** AI Agent (easy to switch providers)
   - **For Images:** Direct HTTP to Stability AI (image models don't work with AI Agents)
   - **Reasoning:** Best of both worlds - flexibility where needed, control where required

3. **Date in SEO:**
   - **Decision:** Include month/day (e.g., "March 12") but NOT year
   - **Reasoning:** Evergreen content, recurring traffic, no self-cannibalization
   - **Alternative Considered:** Generic titles (rejected due to content cannibalization)

4. **Content Structure:**
   - **Decision:** Rich HTML with schema markup, TOC, FAQ
   - **Reasoning:** Better SEO, better UX, better for AI search engines

5. **Temperature Setting:**
   - **Decision:** 0.85 for combined facts + titles
   - **Reasoning:** Balances factual accuracy with creative titles
   - **Alternative Considered:** 0.8 for facts, 0.9 for titles (rejected as unnecessary)

---

## Traffic Projections

### Expected Growth (Date-Specific SEO)

**Month 1 (30 posts):**
- 30 posts × 10-20 views/day = 300-600 views/day
- Monthly: ~9,000-18,000 views

**Month 6 (180 posts):**
- 180 posts × 10-20 views/day = 1,800-3,600 views/day
- Monthly: ~54,000-108,000 views

**Year 1 (365 posts):**
- 365 posts × 10-20 views/day = 3,650-7,300 views/day
- Monthly: ~110,000-220,000 views

**Year 2 (same posts, updated):**
- Authority builds, rankings improve
- 365 posts × 20-50 views/day = 7,300-18,250 views/day
- Monthly: ~220,000-550,000 views

### Revenue Potential

**With Display Ads (₹100-300 per 1000 views):**
- Year 1: ₹11,000-66,000/month
- Year 2: ₹22,000-165,000/month

**ROI:**
- Cost: ₹349-555/month
- Break-even: ~3,500-5,500 views/month
- Achieved by: Month 1-2

---

## Troubleshooting

### Common Issues

**1. JSON Parsing Errors:**
- **Cause:** AI returns markdown code blocks
- **Solution:** Parse & Validate node strips ```json``` blocks
- **Check:** Temperature not too high (keep at 0.85)

**2. Image Generation Fails:**
- **Cause:** Invalid API key or rate limiting
- **Solution:** Verify Stability AI key, check rate limits
- **Workaround:** Add retry logic or reduce to 3 images

**3. WordPress Upload Fails:**
- **Cause:** Auth issues or file size limits
- **Solution:** Check Application Password, verify WP config
- **Check:** `upload_max_filesize` in php.ini

**4. Duplicate Facts:**
- **Cause:** No database tracking
- **Solution:** Manual review or add Google Sheets logging
- **Future:** Implement fact deduplication

**5. SEO Meta Not Saving:**
- **Cause:** Yoast SEO not installed
- **Solution:** Install Yoast SEO plugin or remove meta fields

### Validation Checks

Before going live:
- [ ] Test manually with "Execute Workflow"
- [ ] Verify all 5 images generate
- [ ] Check WordPress post appears correctly
- [ ] Validate schema markup (Google Rich Results Test)
- [ ] Test on mobile devices
- [ ] Check page load speed
- [ ] Verify internal links work
- [ ] Test with different dates

---

## File Information

### Workflow File

**Filename:** `car-facts-wordpress-seo-geo-single-call.json`

**Version:** Optimized single-call version

**n8n Compatibility:** n8n v1.0+

**Import Instructions:**
1. Open n8n
2. Go to Workflows → Click "+" → Import from File
3. Select the JSON file
4. Configure credentials
5. Replace placeholder values
6. Test manually
7. Activate workflow

---

## Project History

### Evolution

1. **Initial Version:**
   - Direct HTTP requests to Azure OpenAI and Stability AI
   - Basic WordPress publishing
   - No SEO optimization

2. **AI Agent Version:**
   - Switched text generation to AI Agent nodes
   - Added flexibility to swap providers
   - Images still via HTTP (agent doesn't support image models)

3. **SEO/GEO Version (Two Calls):**
   - Added second AI call for titles and SEO
   - Implemented full schema markup
   - Added GEO optimization
   - Cost: ₹62-70/month

4. **Optimized Version (Current):**
   - **Combined into single AI call**
   - Same features, lower cost
   - Cost: ₹49-55/month
   - Simplified architecture

### Key Optimizations

- Merged fact generation with SEO/title generation
- Removed redundant nodes
- Optimized prompt for dual purpose
- Maintained all features while reducing costs

---

## Future Roadmap

### Potential Enhancements

**Short-term:**
- [ ] Add Google Sheets logging for duplicate prevention
- [ ] Implement email notifications on publish
- [ ] Add social media auto-posting (Twitter, Facebook)
- [ ] Create weekly digest email

**Medium-term:**
- [ ] Multi-language support (Hindi, Spanish, etc.)
- [ ] Automatic category assignment based on content
- [ ] Image watermarking with blog branding
- [ ] A/B testing for title variations

**Long-term:**
- [ ] Podcast episode generation (AI voice)
- [ ] Video shorts creation (TikTok, YouTube Shorts)
- [ ] Interactive timeline feature
- [ ] User-submitted facts system

---

## Support & Resources

### Documentation Links

- **n8n Documentation:** https://docs.n8n.io/
- **Azure OpenAI API:** https://learn.microsoft.com/en-us/azure/ai-services/openai/
- **Stability AI API:** https://platform.stability.ai/docs
- **WordPress REST API:** https://developer.wordpress.org/rest-api/
- **Schema.org Markup:** https://schema.org/

### Getting Help

- n8n Community Forum: https://community.n8n.io/
- WordPress Support: https://wordpress.org/support/
- GitHub Issues: (if you version control your workflow)

---

## Notes & Reminders

### Important Considerations

1. **API Rate Limits:**
   - Azure OpenAI: Check your deployment quota
   - Stability AI: 100 images per hour on free tier
   - WordPress: No official limits, but server may throttle

2. **Content Quality:**
   - Always review first few generated posts
   - Adjust temperature if facts seem inaccurate
   - Monitor for duplicate content

3. **Legal:**
   - AI-generated content is not copyrighted (in most jurisdictions)
   - Always add unique human commentary or edits
   - Disclose AI usage if required by your jurisdiction

4. **Backup:**
   - Export workflow JSON regularly
   - Backup WordPress database
   - Save generated content externally

5. **Monitoring:**
   - Set up Google Analytics
   - Monitor Google Search Console
   - Track rankings for target keywords

---

## Contact & Credits

**Workflow Created:** March 2026

**Created By:** Alen (with assistance from Claude AI)

**Tools Used:**
- n8n (workflow automation)
- Azure OpenAI (text generation)
- Stability AI (image generation)
- WordPress (publishing platform)

**Special Thanks:**
- n8n community for workflow inspiration
- Anthropic for Claude AI assistance

---

## Changelog

### Version 1.0 (Current)
- Single AI call for facts + SEO + titles
- Full SEO and GEO optimization
- Date-specific evergreen content strategy
- Cost: ₹49-55/month

### Version 0.3
- Two AI calls (facts, then SEO/titles)
- Full SEO/GEO features
- Cost: ₹62-70/month

### Version 0.2
- AI Agent for text generation
- Direct HTTP for images
- Basic SEO

### Version 0.1
- All HTTP requests
- No SEO optimization
- Basic WordPress publishing

---

## License

This workflow is for personal use. Modify as needed for your requirements.

---

**Last Updated:** March 21, 2026
**Document Version:** 1.0
**Status:** Production Ready ✅
