"use client";

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

/**
 * Marquee ticker strip — scrolling car brand names.
 */
export default function Marquee() {
  // Duplicate for seamless infinite scroll
  const items = [...CAR_BRANDS, ...CAR_BRANDS];

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
