import {Inject, Injectable} from '@angular/core';
import {
  NewUserInput,
  UpdateAvatarBody,
  UpdateThemePreferenceBody,
  UpdateUserAddressBody,
  UpdateUserNameBody,
  UserAccountModel,
  UserViewModel
} from "../_models/User";
import {from, Observable, of} from "rxjs";
import {HttpClient, HttpParams} from "@angular/common/http";
import {CookieService} from "ngx-cookie-service";
import {map} from "rxjs/operators";
import {UserPaths} from "../_extensions/api.constants";
import {Address} from "../_models/Address";
import {MappedUsersAnalytics, PageViewEvent, SearchEvent, UsersAnalytics} from "../_models/Analytic";
import {HubConnection, HubConnectionBuilder, HubConnectionState} from "@microsoft/signalr";

interface addressBody
{
  address: Address;
}

interface usernameBody
{
  username: string;
}

interface avatarBody
{
  avatar: string;
}

export interface themePreferenceBody
{
  isDarkMode: boolean
}

@Injectable({
  providedIn: 'root'
})
export class UserService
{
  set User (value: UserAccountModel)
  {
    this._User = value;
  }

  get User (): UserAccountModel
  {
    return this._User;
  }

  get AccountUser (): UserAccountModel
  {
    if (this._User.Email == null)
    {
      this.GetAccountUser(this._User.Id).subscribe(() =>
      {
        return this._User;
      });
    } else
    {
      return this._User;
    }
  }

  private _User: UserAccountModel;
  private readonly BaseUrl: string;

  private connection: HubConnection;

  constructor (private http: HttpClient, @Inject('BASE_URL') baseUrl: string, private cookieService: CookieService)
  {
    this.BaseUrl = baseUrl;
    this.connection = new HubConnectionBuilder().withUrl(this.BaseUrl + 'userHub').build();
  }

  public OpenConnection (): void
  {
    this.connection.start().then(() => {
    //  console.log("Connected to the user hub")
    }).catch(err => console.error('error connecting to user hub', err));

    // this.connection.onclose(error => console.log("disconnected from the user hub"));
  }

  public DestroyConnection (): void
  {
    this.connection.stop().then(r => {});
  }

  public GetUserAsync (): Observable<UserAccountModel | null>
  {
    return of(this._User);
  }

  public CreateUser (newUserInput: NewUserInput): Observable<UserViewModel>
  {
    return this.http.post<UserViewModel>(this.BaseUrl + UserPaths.BasePath, newUserInput);
  }

  public NeedAccountUser (): Observable<UserAccountModel>
  {
    return this._User.Email == null ? this.GetAccountUser(this._User.Id) : of(null);
  }

  public GetAccountUser (id: string): Observable<UserAccountModel>
  {
    return this.http.get<UserAccountModel>(this.BaseUrl + UserPaths.Account, {params: new HttpParams().set('id', id)}).pipe(map(user => this.User = user));
  }

  private FlushUser (): void
  {
    this.User = null;
    this.cookieService.delete('id');
    this.cookieService.delete('token');
    this.cookieService.deleteAll('/');

    console.log(this.cookieService.getAll());
  }

  public UserNameExists (username: string): Observable<boolean>
  {
    if (this.connection.state == HubConnectionState.Connected)
    {
      return from(this.connection.invoke("UserNameExists",username));
    } else {
      return this.http.get<boolean>(this.BaseUrl + UserPaths.UserNameExists, {params: new HttpParams().set('username', username)});
    }
  }

  public EmailExists (email: string): Observable<boolean>
  {
    if (this.connection.state == HubConnectionState.Connected)
    {
      return from(this.connection.invoke("EmailExists", email));
    } else {
      return this.http.get<boolean>(this.BaseUrl + UserPaths.EmailExists, {params: new HttpParams().set('email', email)});
    }
  }

  public PhoneExists (phoneNumber: string): Observable<boolean>
  {
    if (this.connection.state == HubConnectionState.Connected)
    {
      return from(this.connection.invoke("PhoneExists", phoneNumber));
    } else {
      return this.http.get<boolean>(this.BaseUrl + UserPaths.PhoneExists, {params: new HttpParams().set('phoneNumber', phoneNumber)});
    }
  }

  public Destroy (id: string): Observable<any>
  {
    return this.http.delete(this.BaseUrl + UserPaths.BasePath, {params: new HttpParams().set('id', id)}).pipe(
      map(result =>
      {
        this.FlushUser();
        return result;
      }));
  }

  public UpdateUserName (updateUserNameBody: UpdateUserNameBody): Observable<usernameBody | any>
  {
    return this.http.post<usernameBody>(this.BaseUrl + UserPaths.UpdateUserName, updateUserNameBody).pipe(map(result =>
    {
      this.User.UserName = result.username;
      return result;
    }));
  }

  public UpdateAvatar (updateAvatarBody: UpdateAvatarBody): Observable<avatarBody | any>
  {
    return this.http.post<avatarBody>(this.BaseUrl + UserPaths.UpdateAvatar, updateAvatarBody).pipe(map(result =>
    {
      this.User.Avatar = result.avatar;
      return result;
    }));
  }

  public LogOut (): void
  {
    this.FlushUser();
  }

  public UpdateThemePreference (updateThemePreferenceBody: UpdateThemePreferenceBody): Observable<themePreferenceBody | any>
  {
    return this.http.post<themePreferenceBody>(this.BaseUrl + UserPaths.UpdateThemePreference, updateThemePreferenceBody).pipe(map(result =>
    {
      this.User.IsDarkMode = result.isDarkMode;
      return result;
    }));
  }

  public UpdateAddress (updateUserAddressBody: UpdateUserAddressBody): Observable<addressBody>
  {
    return this.http.post<addressBody>(this.BaseUrl + UserPaths.UpdateAddress, updateUserAddressBody).pipe(map(result =>
    {
      this.User.Address = result.address;
      return result;
    }));
  }

  public GetAnalytics (): Observable<MappedUsersAnalytics>
  {
    return this.http.get<UsersAnalytics>(this.BaseUrl + UserPaths.GetUsersAnalytics, {params: new HttpParams().set('id', this.User.Id)}).pipe(map(a =>
    {
      let pageViews: Map<string, PageViewEvent[]> = new Map<string, PageViewEvent[]>();
      a.PageViewEvents.forEach(x =>
      {
        let created = new Date(x.Created);
        let fullDate: string = (new Date(created.getFullYear(), created.getMonth(), created.getDay())).toDateString();
        if (!pageViews.has(fullDate))
        {
          pageViews.set(fullDate, [x]);
        } else
        {
          pageViews.set(fullDate, [...pageViews.get(fullDate), x]);
        }
      });

      let searchEvents: Map<string, SearchEvent[]> = new Map<string, SearchEvent[]>();
      a.SearchEvents.forEach(x =>
      {
        let created = new Date(x.Created);
        let fullDate: string = (new Date(created.getFullYear(), created.getMonth(), created.getDay())).toDateString();
        if (!searchEvents.has(fullDate))
        {
          searchEvents.set(fullDate, [x]);
        } else
        {
          searchEvents.set(fullDate, [...searchEvents.get(fullDate), x]);
        }
      });


      let mapped: MappedUsersAnalytics = {
        PageViewEvents: Array.from(pageViews, ([key, value]) => ({Date: new Date(key), PageViews: value})),
        RecommendationScores: a.RecommendationScores,
        SearchEvents: Array.from(searchEvents, ([key, value]) => ({Date: new Date(key), Searches: value})),
        TicketVerificationEvents: a.TicketVerificationEvents
      }

      return mapped;
    }));
  }
}
