import {AfterViewInit, Directive, ElementRef, NgZone, OnDestroy, Self} from '@angular/core';
import {MatTooltip} from '@angular/material/tooltip';

/**
 * Adds a touch long-press trigger to a Material tooltip without taking control
 * of the pointer gesture. A moving pointer is treated as scrolling and cancels
 * the tooltip before it can open.
 */
@Directive({
  selector: '[appScrollSafeTooltip]'
})
export class ScrollSafeTooltipDirective implements AfterViewInit, OnDestroy {
  private readonly longPressDelayMs = 500;
  private readonly movementTolerancePx = 8;
  private longPressTimer?: number;
  private activePointerId?: number;
  private startX = 0;
  private startY = 0;
  private tooltipWasOpened = false;
  private suppressNextClick = false;

  constructor(
    private readonly elementRef: ElementRef<HTMLElement>,
    private readonly zone: NgZone,
    @Self() private readonly tooltip: MatTooltip
  ) {}

  ngAfterViewInit(): void {
    this.zone.runOutsideAngular(() => {
      const element = this.elementRef.nativeElement;
      element.addEventListener('pointerdown', this.onPointerDown, {passive: true});
      element.addEventListener('pointermove', this.onPointerMove, {passive: true});
      element.addEventListener('pointerup', this.onPointerUp, {passive: true});
      element.addEventListener('pointercancel', this.onPointerCancel, {passive: true});
      element.addEventListener('click', this.onClickCapture, true);
      element.addEventListener('contextmenu', this.onContextMenu);
    });
  }

  ngOnDestroy(): void {
    const element = this.elementRef.nativeElement;
    element.removeEventListener('pointerdown', this.onPointerDown);
    element.removeEventListener('pointermove', this.onPointerMove);
    element.removeEventListener('pointerup', this.onPointerUp);
    element.removeEventListener('pointercancel', this.onPointerCancel);
    element.removeEventListener('click', this.onClickCapture, true);
    element.removeEventListener('contextmenu', this.onContextMenu);
    this.clearLongPressTimer();
  }

  private readonly onPointerDown = (event: PointerEvent): void => {
    if(event.pointerType === 'mouse' || this.tooltip.disabled || !this.tooltip.message.trim())
    {
      return;
    }

    this.clearLongPressTimer();
    this.activePointerId = event.pointerId;
    this.startX = event.clientX;
    this.startY = event.clientY;
    this.tooltipWasOpened = false;

    this.longPressTimer = window.setTimeout(() => {
      if(this.activePointerId !== event.pointerId)
      {
        return;
      }

      this.tooltipWasOpened = true;
      this.suppressNextClick = true;
      this.zone.run(() => this.tooltip.show(0));
    }, this.longPressDelayMs);
  };

  private readonly onPointerMove = (event: PointerEvent): void => {
    if(event.pointerId !== this.activePointerId)
    {
      return;
    }

    if(Math.hypot(event.clientX - this.startX, event.clientY - this.startY) > this.movementTolerancePx)
    {
      this.cancelTouchTooltip();
    }
  };

  private readonly onPointerUp = (event: PointerEvent): void => {
    if(event.pointerId !== this.activePointerId)
    {
      return;
    }

    this.clearLongPressTimer();
    this.activePointerId = undefined;

    if(this.tooltipWasOpened)
    {
      this.zone.run(() => this.tooltip.hide(1500));
      this.tooltipWasOpened = false;
    }
  };

  private readonly onPointerCancel = (event: PointerEvent): void => {
    if(event.pointerId === this.activePointerId)
    {
      this.cancelTouchTooltip();
      this.suppressNextClick = false;
    }
  };

  private readonly onClickCapture = (event: MouseEvent): void => {
    if(!this.suppressNextClick)
    {
      return;
    }

    this.suppressNextClick = false;
    event.preventDefault();
    event.stopImmediatePropagation();
  };

  private readonly onContextMenu = (event: MouseEvent): void => {
    if(this.tooltipWasOpened || this.suppressNextClick)
    {
      event.preventDefault();
    }
  };

  private cancelTouchTooltip(): void {
    this.clearLongPressTimer();
    this.activePointerId = undefined;

    if(this.tooltipWasOpened)
    {
      this.zone.run(() => this.tooltip.hide(0));
      this.tooltipWasOpened = false;
    }
  }

  private clearLongPressTimer(): void {
    if(this.longPressTimer !== undefined)
    {
      window.clearTimeout(this.longPressTimer);
      this.longPressTimer = undefined;
    }
  }
}
