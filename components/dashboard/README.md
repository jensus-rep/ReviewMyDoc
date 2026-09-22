Herkunft: Atelier components/dashboard, Stand 22.09.2026, Commit abe762535585569f69b7aa81e594c46b09be9681

# Dashboard 1.0.0

Kennzahlenkarten, Inhaltsbereiche, Verteilungsdiagramm und Prozessschritte. Frameworkfrei und ohne JavaScript. Farben, Abstände und Schrift kommen aus Tokens; `--dashboard-surface`, `--dashboard-accent`, `--dashboard-radius` und `--dashboard-value-size` lassen sich überschreiben.

`demo.html` zeigt Beispieldaten. Im Admin-Dashboard werden Bestandszahlen aus dem Kundendienst angezeigt. Das SVG besitzt eine Textalternative; Zahlen stehen zusätzlich lesbar an den Balken. Razor-Karte: `Pages/Shared/Ui/_DashboardMetric.cshtml` mit `DashboardMetricModel`. Kleine Ansichten stapeln die Bereiche, große zeigen vier Kennzahlen nebeneinander.
