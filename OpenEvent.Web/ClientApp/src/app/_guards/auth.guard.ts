import {Injectable} from '@angular/core';
import {
  CanActivate,
  ActivatedRouteSnapshot,
  RouterStateSnapshot,
  Router,
  CanActivateChild
} from '@angular/router';
import {Observable} from 'rxjs';
import {AuthService} from "../_Services/auth.service";
import {tap} from "rxjs/operators";

@Injectable({
  providedIn: 'root'
})
export class AuthGuard implements CanActivate, CanActivateChild
{

  constructor (private authService: AuthService, private router: Router)
  {
  }

  public canActivate (
    _next: ActivatedRouteSnapshot,
    state: RouterStateSnapshot): Observable<boolean>
  {
    return this.authService.IsAuthenticated().pipe(tap(isAuthenticated =>
    {
      if (!isAuthenticated) this.router.navigate(['/'])
      return isAuthenticated;
    }));
  }

  canActivateChild (childRoute: ActivatedRouteSnapshot, state: RouterStateSnapshot): Observable<boolean>
  {
    return this.canActivate(childRoute,state);
  }

}
