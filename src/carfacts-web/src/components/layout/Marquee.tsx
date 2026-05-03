"use client";
import { useMemo } from "react";

const CAR_BRANDS = [
  "Mercedes-Benz",
  "BMW",
  "Tesla",
  "Ferrari",
  "Porsche",
  "Lamborghini",
  "Rolls-Royce",
  "Bentley",
  "Audi",
  "Toyota",
  "Honda",
  "Ford",
  "Chevrolet",
  "Dodge",
  "Bugatti",
  "McLaren",
  "Aston Martin",
  "Maserati",
  "Alfa Romeo",
  "Jaguar",
  "Land Rover",
  "Volvo",
  "Subaru",
  "Mazda",
  "Nissan",
  "Hyundai",
  "Koenigsegg",
  "Pagani",
  "Lotus",
];

function shuffle<T>(arr: T[]): T[] {
  const a = [...arr];
  for (let i = a.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [a[i], a[j]] = [a[j], a[i]];
  }
  return a;
}

/**
 * Marquee ticker strip — scrolling car brand names.
 * Brands are shuffled on every page load (client-side).
 */
export default function Marquee() {
  const brands = useMemo(() => shuffle(CAR_BRANDS), []);
  // Duplicate for seamless infinite scroll
  const items = [...brands, ...brands];

  return (
    <section
      className="overflow-hidden border-b border-border bg-foreground py-4 text-background select-none"
      aria-hidden="true"
    >
      <div className="flex animate-[marquee_40s_linear_infinite] gap-12 whitespace-nowrap font-display text-2xl font-bold tracking-tight md:text-3xl">
        {items.map((brand, i) => (
          <span key={i} className="flex items-center gap-12">
            {brand}
            <span className="text-signal">●</span>
          </span>
        ))}
      </div>
    </section>
  );
}
