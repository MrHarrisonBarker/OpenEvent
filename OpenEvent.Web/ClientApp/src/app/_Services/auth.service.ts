import {Inject, Injectable} from '@angular/core';
import {HttpClient} from "@angular/common/http";
import {Observable, of} from "rxjs";
import {AuthBody, UpdatePasswordBody, UserViewModel} from "../_models/User";
import {map, switchMap} from "rxjs/operators";
import {CookieService} from "ngx-cookie-service";
import jwtDecode, {JwtPayload} from "jwt-decode";
import {UserService} from "./user.service";
import {AuthPaths} from "../_extensions/api.constants";
import {TriggerService} from "./trigger.service";

@Injectable({
  providedIn: 'root'
})
export class AuthService
{

  private Token: string;
  private readonly BaseUrl: string;

  constructor (
    private http: HttpClient,
    @Inject('BASE_URL') baseUrl: string,
    private cookieService: CookieService,
    private userService: UserService,
    private trigger: TriggerService
  )
  {
    this.BaseUrl = baseUrl;
  }

  public Login (auth: AuthBody): Observable<UserViewModel>
  {
    return this.http.post<UserViewModel>(this.BaseUrl + AuthPaths.Login, auth).pipe(
      map(user =>
      {
        console.log(user);
        this.Token = user.Token;
        let payload: JwtPayload = jwtDecode(this.Token);
        this.cookieService.set('token', this.Token, new Date(payload.exp * 1000), '/', 'localhost');
        this.cookieService.set('id', user.Id, new Date(payload.exp * 1000), '/', 'localhost');
        this.userService.User = user;
        return user;
      })
    );
  }

  public Authenticate (id: string): Observable<UserViewModel>
  {
    return this.http.post<UserViewModel>(this.BaseUrl + AuthPaths.Authenticate, {id: id}).pipe(map(user =>
    {
      this.userService.User = user;
      this.trigger.isDark.emit(user.IsDarkMode);
      console.log(this.userService.User);
      return user;
    }));
  }

  public IsAuthenticated (): Observable<boolean>
  {
    return this.userService.GetUserAsync().pipe(
      switchMap(u =>
        {
          if (u == null)
          {
            console.log("There is no user");
            let id = this.cookieService.get('id');
            if (this.cookieService.check('id'))
            {
              console.log("Client has token saved, getting user", id);
              return this.Authenticate(id).pipe(map(x => !!x));
            }
            return of(false);
          }
          return of(true);
        }
      ));
  }

  public GetToken (): string
  {
    return this.cookieService.get('token');
  }

  public UpdatePassword (updatePasswordBody: UpdatePasswordBody): Observable<any>
  {
    return this.http.post<string>(this.BaseUrl + AuthPaths.UpdatePassword, updatePasswordBody);
  }
}
