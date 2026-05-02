import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/**
 * Initials-in-a-circle avatar. Phase 12 placeholder; phase 14 (or later) can
 * replace this with a Microsoft Graph profile picture without changing the
 * call sites — same selector and inputs.
 */
@Component({
  selector: 'lumo-avatar',
  template: `
    <div class="avatar" [class.avatar--lg]="size() === 'lg'" [attr.aria-label]="name()">
      <span aria-hidden="true">{{ initials() }}</span>
    </div>
  `,
  styleUrl: './avatar.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AvatarComponent {
  readonly name = input.required<string>();
  readonly size = input<'md' | 'lg'>('md');

  protected readonly initials = computed(() => {
    const trimmed = this.name().trim();
    if (!trimmed) return '?';
    const parts = trimmed.split(/\s+/).filter(Boolean);
    if (parts.length >= 2) {
      return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
    }
    return parts[0].slice(0, 2).toUpperCase();
  });
}
