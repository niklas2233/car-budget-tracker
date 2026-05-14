#!/usr/bin/env node

/**
 * Car.info license-plate page scraper using Playwright.
 *
 * Usage:
 *   node tools/car_info_scraper.mjs https://www.car.info/sv-se/license-plate/S/RHZ927#specs
 */

import { chromium } from 'playwright';
import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { load } from 'cheerio';

const DEFAULT_URL = "https://www.car.info/sv-se/license-plate/S/RHZ927#specs";

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function fetchWithPlaywright(url, attempts = 5) {
  let lastError = null;
  let browser = null;

  for (let i = 0; i < attempts; i += 1) {
    const waitMs = 1000 * Math.pow(2, i);

    try {
      browser = await chromium.launch({ headless: true });
      const context = await browser.newContext({
        userAgent: "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36",
        locale: 'sv-SE',
      });

      const page = await context.newPage();
      const response = await page.goto(url, { waitUntil: 'networkidle', timeout: 30000 });

      if (!response || !response.ok()) {
        const statusError = new Error(`HTTP ${response?.status() ?? 'unknown'}`);
        lastError = statusError;
        await context.close();
        await browser.close();
        browser = null;
        
        if (i < attempts - 1) {
          await sleep(waitMs);
          continue;
        }

        throw statusError;
      }

      // Wait for specs section to be rendered
      await page.waitForSelector('#specifications .sprow', { timeout: 5000 }).catch(() => {
        // Specs section might not exist, that's okay
      });

      // Python parity: give dynamic sections a short settle window.
      await page.waitForTimeout(2000);

      const domFacts = await page.evaluate(() => {
        const toText = (value) => (value ?? "").replace(/\s+/g, " ").trim();
        const facts = {};

        const rows = Array.from(document.querySelectorAll('.sprow'));
        for (const row of rows) {
          const spans = row.querySelectorAll('span.ast-i');
          if (spans.length < 2) continue;

          const label = toText(spans[0].textContent).replace(/\s*:\s*$/, '');
          const value = toText(spans[1].textContent);
          if (!label || !value || label === value) continue;

          facts[label] = value;
        }

        const looseLabels = Array.from(document.querySelectorAll('span.sptitle'));
        for (const labelNode of looseLabels) {
          const label = toText(labelNode.textContent).replace(/\s*:\s*$/, '');
          const sibling = labelNode.nextElementSibling;
          const value = toText(sibling?.textContent ?? '');
          if (!label || !value || label === value) continue;
          if (!facts[label]) facts[label] = value;
        }

        return facts;
      });

      const html = await page.content();
      await context.close();
      await browser.close();
      
      return { html, domFacts };
    } catch (error) {
      lastError = error;
      if (browser) {
        await browser.close().catch(() => {});
      }
      if (i < attempts - 1) {
        await sleep(waitMs);
      }
    }
  }

  throw lastError ?? new Error("Failed to fetch page");
}

function decodeHtmlEntities(str) {
  return str
    .replace(/&nbsp;/g, " ")
    .replace(/&amp;/g, "&")
    .replace(/&quot;/g, '"')
    .replace(/&#39;/g, "'")
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">");
}

function normalizeText(str) {
  return decodeHtmlEntities(str)
    .replace(/<script[\s\S]*?<\/script>/gi, " ")
    .replace(/<style[\s\S]*?<\/style>/gi, " ")
    .replace(/<[^>]+>/g, " ")
    .replace(/\s+/g, " ")
    .trim();
}

function extractMeta(html, attr, value) {
  const metaRe = new RegExp(`<meta[^>]*${attr}=["']${value}["'][^>]*content=["']([^"']+)["'][^>]*>`, "i");
  const fallbackRe = new RegExp(`<meta[^>]*content=["']([^"']+)["'][^>]*${attr}=["']${value}["'][^>]*>`, "i");

  const m1 = html.match(metaRe);
  if (m1) return decodeHtmlEntities(m1[1].trim());

  const m2 = html.match(fallbackRe);
  if (m2) return decodeHtmlEntities(m2[1].trim());

  return null;
}

function extractTitle(html) {
  const m = html.match(/<title>([\s\S]*?)<\/title>/i);
  return m ? decodeHtmlEntities(m[1].trim()) : null;
}

function extractCanonical(html) {
  const m = html.match(/<link[^>]*rel=["']canonical["'][^>]*href=["']([^"']+)["'][^>]*>/i);
  return m ? m[1].trim() : null;
}

function extractJsonLd(html) {
  const blocks = [...html.matchAll(/<script[^>]*type=["']application\/ld\+json["'][^>]*>([\s\S]*?)<\/script>/gi)];
  const parsed = [];

  for (const block of blocks) {
    const raw = block[1]?.trim();
    if (!raw) continue;

    try {
      parsed.push(JSON.parse(raw));
    } catch {
      // Ignore invalid JSON-LD blocks.
    }
  }

  return parsed;
}

function cleanFactValue(value) {
  if (!value) return null;

  const cleaned = value
    .replace(/^[:\-\s]+/, "")
    .replace(/\s{2,}/g, " ")
    .trim();

  if (!cleaned) return null;

  // Guard against contamination from embedded script/JSON/URLs in fallback text.
  if (/[{}\[\]]/.test(cleaned)) return null;
  if (/https?:\/\//i.test(cleaned)) return null;
  if (cleaned.length > 140) return null;

  return cleaned;
}

function extractSpecsRows(html) {
  const $ = load(html);
  const facts = {};

  $('.sprow').each((_, row) => {
    const spans = $(row).find('span.ast-i');
    if (spans.length < 2) return;

    const label = normalizeText($(spans[0]).text()).replace(/\s*:\s*$/, "").trim();
    const value = cleanFactValue(normalizeText($(spans[1]).text()));
    if (!label || !value) return;
    if (label === value) return;

    facts[label] = value;
  });

  return facts;
}

function extractFactsFromText(text) {
  const labels = [
    "I trafik",
    "Färg",
    "Antal ägare",
    "Mätarställning",
    "Kaross",
    "Klassificering",
    "Generation",
    "Motor",
    "Växellåda",
    "Drivlina",
    "Utrustningsnivå",
    "Blandad förbrukning",
    "CO₂, Blandad",
  ];

  const facts = {};
  const points = [];

  for (const label of labels) {
    const idx = text.indexOf(label);
    if (idx >= 0) points.push({ label, idx });
  }

  points.sort((a, b) => a.idx - b.idx);

  for (let i = 0; i < points.length; i += 1) {
    const current = points[i];
    const valueStart = current.idx + current.label.length;
    const valueEnd = i < points.length - 1 ? points[i + 1].idx : Math.min(text.length, valueStart + 120);
    const rawValue = text.slice(valueStart, valueEnd).trim();

    const cleaned = cleanFactValue(rawValue);
    if (cleaned) facts[current.label] = cleaned;
  }

  return facts;
}

function getFact(facts, ...labels) {
  for (const label of labels) {
    if (facts[label]) return facts[label];
  }

  return null;
}

function getMainEntityFromJsonLd(jsonLd) {
  for (const block of jsonLd) {
    if (block?.mainEntity && typeof block.mainEntity === "object") {
      return block.mainEntity;
    }
  }

  return null;
}

function cleanVehicleField(value) {
  if (!value) return null;

  const normalized = String(value)
    .replace(/\s+Rapporterad stulen.*$/i, "")
    .replace(/\s+Läs mer och beställ.*$/i, "")
    .replace(/\s+Chassinummer.*$/i, "")
    .replace(/\s+\".*$/i, "")
    .trim();

  const cleaned = cleanFactValue(normalized);
  if (!cleaned) return null;

  const contamination = [
    "data-",
    "\">",
    "Läs mer och beställ",
    "Rapporterad stulen",
    "Logga in",
  ];

  for (const marker of contamination) {
    if (cleaned.includes(marker)) return null;
  }

  return cleaned;
}

function toTrafficFlag(value) {
  const cleaned = cleanVehicleField(value);
  if (!cleaned) return null;
  if (/\bja\b/i.test(cleaned)) return "Ja";
  if (/\bnej\b/i.test(cleaned)) return "Nej";
  return cleaned;
}

function toOwnersCount(value) {
  const cleaned = cleanVehicleField(value);
  if (!cleaned) return null;
  const match = cleaned.match(/\d+/);
  return match ? match[0] : cleaned;
}

function toFuelConsumption(value) {
  const cleaned = cleanVehicleField(value);
  if (!cleaned) return null;
  const match = cleaned.match(/\d+[\d,.]*\s*l\/100km/i);
  return match ? match[0].replace(/\s+/g, " ") : cleaned;
}

function toCo2(value) {
  const cleaned = cleanVehicleField(value);
  if (!cleaned) return null;
  const match = cleaned.match(/\d+[\d,.]*\s*g\/km/i);
  return match ? match[0].replace(/\s+/g, " ") : cleaned;
}

function toTrimLevel(value) {
  const cleaned = cleanVehicleField(value);
  if (!cleaned) return null;
  const withoutMetrics = cleaned
    .replace(/\s+\d+[\d,.]*\s*l\/100km.*$/i, "")
    .trim();
  return withoutMetrics || null;
}

function buildOutput(url, html, domFacts = {}) {
  const title = extractTitle(html);
  const description = extractMeta(html, "property", "og:description")
    ?? extractMeta(html, "name", "description");
  const ogTitle = extractMeta(html, "property", "og:title");
  const ogImage = extractMeta(html, "property", "og:image");
  const canonical = extractCanonical(html);
  const jsonLd = extractJsonLd(html);
  const mainEntity = getMainEntityFromJsonLd(jsonLd);

  const specFacts = extractSpecsRows(html);
  const pageText = normalizeText(html);
  const fallbackFacts = extractFactsFromText(pageText);
  const facts = { ...fallbackFacts, ...specFacts, ...domFacts };

  let plate = null;
  const plateFromTitle = (title ?? "").match(/^([A-Z0-9]{5,8})\s*-/i);
  if (plateFromTitle) plate = plateFromTitle[1].toUpperCase();

  const output = {
    sourceUrl: url,
    scrapedAt: new Date().toISOString(),
    page: {
      title,
      ogTitle,
      description,
      canonical,
      ogImage,
    },
    vehicle: {
      plate,
      makeModelFromTitle: title ? title.replace(/^([A-Z0-9]{5,8})\s*-\s*/i, "") : null,
      inTraffic: toTrafficFlag(getFact(facts, "I trafik")) ?? null,
      color: cleanVehicleField(getFact(facts, "Färg", "Color") ?? mainEntity?.color ?? null),
      owners: toOwnersCount(getFact(facts, "Antal ägare", "Previous owners") ?? mainEntity?.numberOfPreviousOwners ?? null),
      mileage: cleanVehicleField(getFact(facts, "Mätarställning", "Mileage") ?? mainEntity?.mileageFromOdometer?.value ?? null),
      bodyType: cleanVehicleField(getFact(facts, "Kaross", "Body type") ?? mainEntity?.bodyType ?? null),
      classification: cleanVehicleField(getFact(facts, "Klassificering")) ?? null,
      generation: cleanVehicleField(getFact(facts, "Generation")) ?? null,
      engine: cleanVehicleField(getFact(facts, "Motor", "Engine")) ?? null,
      gearbox: cleanVehicleField(getFact(facts, "Växellåda", "Gearbox")) ?? null,
      driveTrain: cleanVehicleField(getFact(facts, "Drivlina", "Drive train")) ?? null,
      trimLevel: toTrimLevel(getFact(facts, "Utrustningsnivå", "Trim level")) ?? null,
      fuelConsumptionMixed: toFuelConsumption(getFact(facts, "Blandad förbrukning", "Fuel consumption mixed")) ?? null,
      co2Mixed: toCo2(getFact(facts, "CO₂, Blandad", "CO₂, Mixed")) ?? null,
    },
    specifications: facts,
    jsonLd,
  };

  return output;
}

async function main() {
  const input = process.argv[2] ?? DEFAULT_URL;

  try {
    const isLocalHtml = /\.html?$/i.test(input);

    if (isLocalHtml) {
      const htmlPath = resolve(process.cwd(), input);
      const html = await readFile(htmlPath, 'utf8');
      const data = buildOutput(`file://${htmlPath}`, html);
      process.stdout.write(`${JSON.stringify(data, null, 2)}\n`);
      return;
    }

    const { html, domFacts } = await fetchWithPlaywright(input);
    const data = buildOutput(input, html, domFacts);
    process.stdout.write(`${JSON.stringify(data, null, 2)}\n`);
  } catch (error) {
    console.error("Scrape failed:", error?.message ?? error);
    process.exit(1);
  }
}

main();
