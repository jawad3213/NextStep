import { Component, inject, OnInit } from '@angular/core';
import { AuthService } from '@core/auth/auth.service';

@Component({
  selector: 'app-signup',
  standalone: true,
  template: '',
})
export class SignupComponent implements OnInit {
  private readonly authService = inject(AuthService);

  ngOnInit() {
    this.authService.register();
  }
}
