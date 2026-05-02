/**
 * Returns a Finnish greeting that matches the local time of day.
 *
 * Buckets follow common Finnish convention:
 *   05:00–10:00  → "Hyvää huomenta"
 *   10:00–18:00  → "Hyvää päivää"
 *   18:00–22:00  → "Hyvää iltaa"
 *   22:00–05:00  → "Hyvää yötä"
 */
export function greetingFor(date: Date = new Date()): string {
  const hour = date.getHours();
  if (hour >= 5 && hour < 10) return 'Hyvää huomenta';
  if (hour >= 10 && hour < 18) return 'Hyvää päivää';
  if (hour >= 18 && hour < 22) return 'Hyvää iltaa';
  return 'Hyvää yötä';
}
