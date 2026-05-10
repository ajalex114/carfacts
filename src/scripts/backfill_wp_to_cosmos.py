"""
Backfill WordPress posts (May 2-10, 2026) into Cosmos DB.

The blog publishes to both WordPress and Cosmos DB, but Cosmos save has been
silently failing since May 2. This script fetches the missing posts from the
WordPress public API and upserts them into Cosmos DB via its REST API.
"""

import json
import subprocess
import sys
import urllib.request
import urllib.error
import urllib.parse
import re
from datetime import datetime, timezone
from html import unescape


WP_API_URL = (
    "https://public-api.wordpress.com/rest/v1.1/sites/carfacts5.wordpress.com/posts/"
    "?after=2026-05-01T00:00:00Z&number=15&order_by=date&order=ASC"
    "&fields=ID,title,date,URL,slug,content,excerpt,tags,categories,featured_image"
)

COSMOS_ACCOUNT = "cosmos-carfacts5"
COSMOS_DB = "carfacts"
COSMOS_CONTAINER = "posts"
COSMOS_ENDPOINT = f"https://{COSMOS_ACCOUNT}.documents.azure.com"
COSMOS_DOCS_URL = f"{COSMOS_ENDPOINT}/dbs/{COSMOS_DB}/colls/{COSMOS_CONTAINER}/docs"

CUTOFF_START = datetime(2026, 5, 2, tzinfo=timezone.utc)
CUTOFF_END = datetime(2026, 5, 10, 23, 59, 59, tzinfo=timezone.utc)


def get_aad_token() -> str:
    """Obtain an AAD token for Cosmos DB via az CLI."""
    print("Fetching AAD token via az account get-access-token ...")
    cmd = (
        f'az account get-access-token'
        f' --resource https://{COSMOS_ACCOUNT}.documents.azure.com'
        f' -o tsv --query accessToken'
    )
    result = subprocess.run(cmd, capture_output=True, text=True, check=True, shell=True)
    token = result.stdout.strip()
    if not token:
        raise RuntimeError("Failed to obtain AAD token (empty response)")
    print(f"Token obtained ({len(token)} chars)")
    return token


def fetch_wp_posts() -> list[dict]:
    """Fetch posts from the WordPress public API."""
    print(f"Fetching posts from WordPress API ...")
    req = urllib.request.Request(WP_API_URL)
    with urllib.request.urlopen(req, timeout=30) as resp:
        data = json.loads(resp.read().decode())

    all_posts = data.get("posts", [])
    print(f"  WordPress returned {len(all_posts)} post(s)")

    filtered = []
    for p in all_posts:
        post_date = datetime.fromisoformat(p["date"].replace("Z", "+00:00"))
        if post_date.tzinfo is None:
            post_date = post_date.replace(tzinfo=timezone.utc)
        if CUTOFF_START <= post_date <= CUTOFF_END:
            filtered.append(p)

    print(f"  {len(filtered)} post(s) in range May 2-10, 2026")
    return filtered


def strip_html(html: str) -> str:
    """Crude strip of HTML tags for excerpt/meta text."""
    text = re.sub(r"<[^>]+>", "", html)
    text = unescape(text).strip()
    # collapse whitespace
    text = re.sub(r"\s+", " ", text)
    return text


def sanitize_slug(slug: str) -> str:
    """Ensure slug contains only lowercase alphanumeric and hyphens."""
    slug = slug.lower().strip()
    slug = re.sub(r"[^a-z0-9-]", "-", slug)
    slug = re.sub(r"-+", "-", slug).strip("-")
    return slug


def wp_to_cosmos_doc(wp_post: dict) -> dict:
    """Convert a WordPress post to the Cosmos PostDocument schema."""
    post_date = datetime.fromisoformat(wp_post["date"].replace("Z", "+00:00"))
    if post_date.tzinfo is None:
        post_date = post_date.replace(tzinfo=timezone.utc)

    slug = sanitize_slug(wp_post["slug"])
    yyyy = post_date.strftime("%Y")
    mm = post_date.strftime("%m")
    dd = post_date.strftime("%d")
    iso_date = post_date.strftime("%Y-%m-%dT%H:%M:%S.000Z")

    excerpt_text = strip_html(wp_post.get("excerpt", "") or "")

    # Extract tags from WordPress
    wp_tags = wp_post.get("tags", {}) or {}
    tag_list = [t for t in wp_tags.keys()] if isinstance(wp_tags, dict) else []

    featured_image = wp_post.get("featured_image", "") or ""

    return {
        "id": f"{yyyy}-{mm}-{dd}_{slug}",
        "partitionKey": f"{yyyy}-{mm}",
        "slug": slug,
        "postUrl": f"https://carfactsdaily.com/{yyyy}/{mm}/{dd}/{slug}/",
        "title": unescape(wp_post.get("title", "")),
        "metaDescription": excerpt_text,
        "excerpt": excerpt_text,
        "htmlContent": wp_post.get("content", ""),
        "featuredImageUrl": featured_image,
        "images": [],
        "keywords": tag_list,
        "tags": tag_list,
        "socialHashtags": [],
        "category": "car-facts",
        "author": "thecargeek",
        "publishedAt": iso_date,
        "geoSummary": "",
        "facts": [],
        "wordPressPostId": wp_post["ID"],
        "wordPressPostUrl": wp_post.get("URL", ""),
        "createdAt": iso_date,
    }


def upsert_to_cosmos(doc: dict, token: str) -> None:
    """Upsert a single document into Cosmos DB via REST API."""
    partition_key = doc["partitionKey"]
    auth_header = f"type%3Daad%26ver%3D1.0%26sig%3D{token}"

    headers = {
        "Authorization": auth_header,
        "x-ms-version": "2018-12-31",
        "Content-Type": "application/json",
        "x-ms-documentdb-partitionkey": json.dumps([partition_key]),
        "x-ms-documentdb-is-upsert": "True",
    }

    body = json.dumps(doc).encode("utf-8")
    req = urllib.request.Request(COSMOS_DOCS_URL, data=body, headers=headers, method="POST")

    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            status = resp.status
            print(f"    Cosmos upsert OK (HTTP {status})")
    except urllib.error.HTTPError as e:
        error_body = e.read().decode() if e.fp else ""
        print(f"    Cosmos upsert FAILED (HTTP {e.code}): {error_body[:300]}")
        raise


def query_cosmos_count(token: str) -> int:
    """Query Cosmos DB to count posts with partitionKey 2026-05."""
    auth_header = f"type%3Daad%26ver%3D1.0%26sig%3D{token}"
    query = {
        "query": "SELECT VALUE COUNT(1) FROM c WHERE c.partitionKey = '2026-05'",
    }
    headers = {
        "Authorization": auth_header,
        "x-ms-version": "2018-12-31",
        "Content-Type": "application/query+json",
        "x-ms-documentdb-partitionkey": json.dumps(["2026-05"]),
        "x-ms-documentdb-isquery": "True",
    }
    body = json.dumps(query).encode("utf-8")
    req = urllib.request.Request(COSMOS_DOCS_URL, data=body, headers=headers, method="POST")

    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            data = json.loads(resp.read().decode())
            docs = data.get("Documents", [])
            return docs[0] if docs else -1
    except urllib.error.HTTPError as e:
        error_body = e.read().decode() if e.fp else ""
        print(f"  Verification query failed (HTTP {e.code}): {error_body[:300]}")
        return -1


def main():
    # 1. Fetch WordPress posts
    wp_posts = fetch_wp_posts()
    if not wp_posts:
        print("No posts found in the specified date range. Exiting.")
        sys.exit(1)

    # 2. Get AAD token
    token = get_aad_token()

    # 3. Convert and upsert each post
    success = 0
    failed = 0
    for i, wp_post in enumerate(wp_posts, 1):
        doc = wp_to_cosmos_doc(wp_post)
        print(f"[{i}/{len(wp_posts)}] Upserting: {doc['id']}  \"{doc['title']}\"")
        try:
            upsert_to_cosmos(doc, token)
            success += 1
        except Exception as e:
            print(f"    ERROR: {e}")
            failed += 1

    print(f"\nDone: {success} succeeded, {failed} failed out of {len(wp_posts)} posts.")

    # 4. Verify
    if success > 0:
        print("\nVerifying: querying Cosmos for May 2026 posts ...")
        count = query_cosmos_count(token)
        if count >= 0:
            print(f"  Cosmos DB has {count} post(s) with partitionKey '2026-05'")
        else:
            print("  Could not verify (query failed).")

    if failed > 0:
        sys.exit(1)


if __name__ == "__main__":
    main()
