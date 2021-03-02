import { ComponentFixture, TestBed } from '@angular/core/testing';

import { TicketPurchaseDialogComponent } from './ticket-purchase-dialog.component';

describe('TicketPurchaseDialogComponent', () => {
  let component: TicketPurchaseDialogComponent;
  let fixture: ComponentFixture<TicketPurchaseDialogComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ TicketPurchaseDialogComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(TicketPurchaseDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});