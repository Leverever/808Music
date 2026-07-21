import {
  AfterContentInit,
  AfterViewInit,
  ChangeDetectorRef,
  Component,
  Input,
  OnChanges,
  OnInit,
  SimpleChanges
} from '@angular/core';

@Component({
  selector: 'pass-strength-meter',
  templateUrl: './pass-strength-meter.component.html',
  styleUrl: './pass-strength-meter.component.css'
})
export class PassStrengthMeterComponent implements OnInit, AfterViewInit, OnChanges {

  ngOnChanges(changes: SimpleChanges): void {
    this.strengthPoints = 0;
    this.data = this.fillMeter(this.password);
  }

  constructor(private cdRef: ChangeDetectorRef) {
  }

  ngAfterViewInit(): void {
  }

  ngOnInit(): void {
  }


  @Input('password') password: string = "";
  strengthText : string = "";
  strengthPoints : number = 0;
  data = {
    text: "",
    css: {}
  }

  fillMeter(passwordBind: string): any {
    if(passwordBind.length >= 8) {
      this.strengthPoints++;
    }
    if(passwordBind.length >= 12 && /[@$!%*?&]{2,}/.test(passwordBind)) {
      this.strengthPoints++;
    }
    if(/[a-z]/.test(passwordBind)) {
      this.strengthPoints++;
    }
    if(/[A-Z]/.test(passwordBind)) {
      this.strengthPoints++;
    }
    if(/[0-9]/.test(passwordBind)) {
      this.strengthPoints++;
    }
    if(/[@$!%*?&]/.test(passwordBind)) {
      this.strengthPoints++;
    }

    switch (this.strengthPoints) {
        case 0:
          return {
            text: "",
            css: {
            'width': '0%',
          }}
        case 1:
          return {
            text: "Password is very weak.",
            css: {
            'width': '10%',
            'background-color': '#ff6f91',
          }}
        case 2:
          return {
            text: "Password is weak.",
            css: {
            'width': '30%',
            'background-color': '#ff967a',
          }}
        case 3:
          return {
            text: "Password is moderate.",
            css: {
            'width': '60%',
            'background-color': '#efbb62',
          }}
        case 4:
          return {
            text: "Password is above moderate.",
            css: {
            'width': '80%',
            'background-color': '#d887ed',
          }}
        case 5:
          return {
            text: "Password is strong.",
            css: {
            'width': '90%',
            'background-color': '#9adfae',
          }}
        case 6:
          return {
            text: "Password is very strong.",
            css: {
            'width': '100%',
            'background-color': '#65d995',
          }}
    }
  }
}
