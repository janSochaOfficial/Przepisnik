# Seed data sources

Values are rough, rounded household figures taken from general cooking conversion tables. They are meant for shopping and
scaling, not for nutrition.

- `gramsPerMl` (density): common Polish and international "grams per cup/spoon" kitchen tables (for example the
  conversion charts of przepisy.pl and King Arthur Baking's ingredient weight chart). Flour 0.53, sugar 0.85, icing sugar 0.6,
  butter 0.91, honey 1.42, rice 0.85, salt 1.2, vegetable oil 0.92, olive oil 0.91, milk 1.03, cream 1.0, spices 0.15 to 0.6
  depending on grind (ground spices are light, salt is heavy).
- `gramsPerPiece`: average market sizes from the same tables and the Polish food composition tables (Instytut Zywnosci i
  Zywienia, "Tabele skladu i wartosci odzywczej zywnosci"), "medium" size. Onion 110 g, carrot 70 g, potato 150 g, garlic
  clove 5 g, egg 55 g (size M), tomato 120 g, lemon 100 g. For canned goods the value is the typical net content of one can.
- `defaultPackageAmount`: typical shelf package sizes in Polish supermarkets (cream 330 ml, milk 1000 ml, butter 200 g,
  natural yogurt 400 g, passata 500 ml, yellow cheese 150 g, eggs 10 pcs, canned goods 1 can).

Where a product has no entry for a field, there is no reliable common figure; users can edit it later.
