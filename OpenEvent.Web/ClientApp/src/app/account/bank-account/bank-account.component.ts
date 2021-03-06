import {Component, OnInit, ViewChild} from '@angular/core';
import {StripeIbanComponent, StripeService} from "ngx-stripe";
import {
  CreateTokenBankAccountData,
  CreateTokenIbanData,
  StripeIbanElementChangeEvent,
  StripeIbanElementOptions
} from "@stripe/stripe-js";
import {UserService} from "../../_Services/user.service";
import {BankingService, StripeFilePurpose} from "../../_Services/banking.service";
import {HttpErrorResponse} from "@angular/common/http";
import {Signal} from "../../signal/Signal";
import {Balance} from "../../_models/BankAccount";
import {TriggerService} from "../../_Services/trigger.service";
import {IteratorStatus} from "../../_extensions/iterator/iterator.component";
import {FormControl, FormGroup, Validators} from "@angular/forms";

@Component({
  selector: 'bank-account',
  templateUrl: './bank-account.component.html',
  styleUrls: ['./bank-account.component.css']
})
export class BankAccountComponent implements OnInit
{

  @ViewChild(StripeIbanComponent) bank: StripeIbanComponent;

  public options: StripeIbanElementOptions = {
    supportedCountries: ['SEPA'],
    placeholderCountry: 'GB',
    style: {
      base: {
        iconColor: '#8966e8',
        color: '#41315f',
        fontWeight: '300',
        fontFamily: '"Helvetica Neue", Helvetica, sans-serif',
        fontSize: '18px',
        '::placeholder': {
          color: '#000000'
        }
      }
    }
  };

  public bankAccountForm: FormGroup = new FormGroup({
    sortCode1: new FormControl('', [Validators.required,Validators.pattern(/^[0-9]{2}?$/)]),
    sortCode2: new FormControl('', [Validators.required,Validators.pattern(/^[0-9]{2}?$/)]),
    sortCode3: new FormControl('', [Validators.required,Validators.pattern(/^[0-9]{2}?$/)]),
    accountNumber: new FormControl('', [Validators.required,Validators.pattern(/^[0-9]{8}?$/)])
  });

  public addBankAccountError: string;
  public addBankAccountLoading: boolean = false;
  public removeBankAccountError: string;
  public removeBankAccountLoading: boolean = false;

  public documentLoading: boolean = false;

  public balance: Balance;
  public bankComplete: boolean = false;
  public documentError: string;

  get User ()
  {
    return this.userService.User;
  }

  get bankAccount ()
  {
    return this.userService.User?.BankAccounts ? this.userService.User.BankAccounts[0] : null;
  }

  get paymentStatus ()
  {
    if (this.userService.User?.StripeAccountInfo?.Requirements?.pending_verification?.length > 0)
    {
      return Signal.Pending;
    }
    return this.userService.User?.StripeAccountInfo?.ChargesEnabled ? Signal.Enabled : Signal.Disabled;
  }

  get payoutStatus ()
  {
    if (this.userService.User?.StripeAccountInfo?.Requirements?.pending_verification?.length > 0)
    {
      return Signal.Pending;
    }
    return this.userService.User?.StripeAccountInfo?.PayoutsEnabled ? Signal.Enabled : Signal.Disabled;
  }

  get stripeRequirements ()
  {
    return this.userService.User?.StripeAccountInfo?.Requirements?.currently_due;
  }

  get stripeDisabledReason ()
  {
    return this.userService.User?.StripeAccountInfo?.Requirements?.disabled_reason;
  }

  constructor (private stripeService: StripeService, private userService: UserService, private bankingService: BankingService, private trigger: TriggerService)
  {
  }

  ngOnInit (): void
  {
    if (this.userService.User)
    {
      this.bankingService.GetBalance().subscribe(balance =>
      {
        this.balance = balance;
      });
    }
  }

  onChange (ev: StripeIbanElementChangeEvent)
  {
    this.bankComplete = ev.complete;
  }

  public addBankAccount ()
  {
    this.addBankAccountLoading = true;
    this.stripeService.createToken('bank_account', {
      account_holder_name: this.User.FirstName + " " + this.User.LastName,
      account_holder_type: 'individual',
      routing_number: (this.bankAccountForm.controls.sortCode1.value + this.bankAccountForm.controls.sortCode2.value + this.bankAccountForm.controls.sortCode3.value),
      account_number: this.bankAccountForm.controls.accountNumber.value,
      country: 'GB',
      currency: 'GBP'
    } as CreateTokenBankAccountData).subscribe((result) =>
    {
      this.handleBankAccountResult(result);
    });
  }

  public addIBANBankAccount ()
  {
    this.addBankAccountLoading = true;
    this.stripeService.createToken(this.bank.element, {
      account_holder_name: this.userService.User.FirstName + " " + this.userService.User.LastName,
      account_holder_type: "individual",
      currency: "gbp"
    } as CreateTokenIbanData).subscribe((result) =>
    {
      this.handleBankAccountResult(result);
    });
  }

  private handleBankAccountResult (result)
  {
    if (result.token)
    {
      this.bankingService.AddBankAccount({
        BankToken: result.token.id,
        UserId: this.userService.User.Id
      }).subscribe(() =>
      {
        this.trigger.Iterate('Added bank account', 1000, IteratorStatus.good);
        this.addBankAccountLoading = false;
      }, (e: HttpErrorResponse) =>
      {
        this.addBankAccountError = e.error.Message;
        this.addBankAccountLoading = false;
      });
    } else if (result.error)
    {
      this.addBankAccountError = result.error.message;
      this.addBankAccountLoading = false;
    }
  }

  public removeBankAccount ()
  {
    this.removeBankAccountLoading = true;
    this.bankingService.RemoveBankAccount({
      BankId: this.bankAccount.StripeBankAccountId,
      UserId: this.userService.User.Id
    }).subscribe(response =>
    {
      this.trigger.Iterate('Removed bank account', 1000, IteratorStatus.good);
      this.removeBankAccountLoading = false;
    }, (e: HttpErrorResponse) =>
    {
      this.removeBankAccountError = e.error.Message;
      this.removeBankAccountLoading = false;
    });
  }

  public documentInputEvent (event: any)
  {
    this.documentLoading = true;
    this.bankingService.UploadIdentityDocument(event.target.files[0], StripeFilePurpose.IdentityDocument).subscribe(x =>
    {
      this.bankingService.AttachFrontFile(x.id).subscribe(a =>
      {
        if (a)
        {
          this.documentLoading = false;
          this.trigger.Iterate('Uploaded Identity Document', 1000, IteratorStatus.good)
          if (this.userService.User?.StripeAccountInfo?.Requirements) this.userService.User.StripeAccountInfo.Requirements.currently_due = this.userService.User.StripeAccountInfo.Requirements?.currently_due.filter(x => x != 'individual.verification.document')
        }
      }, (e: HttpErrorResponse) =>
      {
        this.documentLoading = false;
        this.documentError = e.error.Message;
      });
    }, (e: HttpErrorResponse) =>
    {
      this.documentLoading = false;
      this.documentError = e.error.Message;
    });
  }

  public additionalDocumentInputEvent (event: any)
  {
    this.documentLoading = true;
    this.bankingService.UploadIdentityDocument(event.target.files[0], StripeFilePurpose.AdditionalVerification).subscribe(x =>
    {
      this.bankingService.AttachAdditionalFile(x.id).subscribe(a =>
      {
        if (a)
        {
          this.documentLoading = false;
          this.trigger.Iterate('Uploaded Additional Identity Document', 1000, IteratorStatus.good);
          if (this.userService.User?.StripeAccountInfo?.Requirements) this.userService.User.StripeAccountInfo.Requirements.currently_due = this.userService.User.StripeAccountInfo.Requirements?.currently_due.filter(x => x != 'individual.verification.additional_document')
        }
      }, (e: HttpErrorResponse) =>
      {
        this.documentLoading = false;
        this.documentError = e.error.Message;
      });
    }, (e: HttpErrorResponse) =>
    {
      this.documentLoading = false;
      this.documentError = e.error.Message;
    });
  }
}
