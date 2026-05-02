import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'lumo-wordmark',
  template: `
    <span class="wordmark" [class.wordmark--small]="size() === 'sm'">
      Lumo
      @if (showProduct()) {
        <span class="product">mVasu</span>
      }
    </span>
  `,
  styleUrl: './wordmark.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WordmarkComponent {
  readonly size = input<'sm' | 'md' | 'lg'>('md');
  readonly showProduct = input<boolean>(false);
}
