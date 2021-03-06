namespace OpenEvent.Data.Models.PaymentMethod
{
    /// <summary>
    /// Payment method view model
    /// </summary>
    public class PaymentMethodViewModel
    {
        /// <summary>
        /// Stripe card id
        /// </summary>
        public string StripeCardId { get; set; }

        /// <summary>
        /// Name of card
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Card brand eg: Visa
        /// </summary>
        public string Brand { get; set; }

        /// <summary>
        /// Funding type eg: debit, credit
        /// </summary>
        public string Funding { get; set; }

        /// <summary>
        /// Expiration month
        /// </summary>
        public long ExpiryMonth { get; set; }

        /// <summary>
        /// Expiration year
        /// </summary>
        public long ExpiryYear { get; set; }

        /// <summary>
        /// Last four digits of card number
        /// </summary>
        public string LastFour { get; set; }

        /// <summary>
        /// Two digit country code eg: GB
        /// </summary>
        public string Country { get; set; }

        /// <summary>
        /// Identifiable name for card
        /// </summary>
        public string NickName { get; set; }

        /// <summary>
        /// If the method is the default
        /// </summary>
        public bool IsDefault { get; set; }
    }
}