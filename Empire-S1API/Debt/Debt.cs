using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader;
using MelonLoader.Utils;
using Newtonsoft.Json;
using S1API.Logging;
using S1API.Entities.NPCs;
using S1API.GameTime;
using S1API.Money;
using S1API.Entities;

namespace Empire
{

    public class DebtManager
    {
        BlackmarketBuyer buyer;
        public bool paidthisweek = false;
        //paythisweek is set to true
        private void SetPayThisWeek()
        {
            paidthisweek = false;
        }

        public DebtManager(BlackmarketBuyer buyer)
        {
            this.buyer = buyer;
            if (buyer._DealerData.DebtRemaining > 0)
            {
                TimeManager.OnWeekPass += DebtPayment; // Subscribe to the week passed event
                //set paidthisweek to false at the start of each week
                TimeManager.OnWeekPass += SetPayThisWeek;
            }
            SendDebtMessage(0,"initial"); // Send the initial debt message
            CheckIfPaidThisWeek();
        }
        // create a method that uses TimeManager.ElapsedDays which returns integer and finds the nearest highest int (including it) divisible by 7
        public static int GetNearestWeek(int days)
        {
            // Find the nearest week (multiple of 7) that is greater than or equal to days
            int weeks = (int)Math.Ceiling((days+1) / 7.0);
            return weeks * 7;
        }
        public void SendDebtMessage(int paymentAmount, string source)
        {
            float debtPayable = buyer.Debt.DayMultiple * (float)Math.Pow(GetNearestWeek(TimeManager.ElapsedDays), buyer.Debt.DayExponent);
            //If DebtRemaining<debtPayable, set debtPayable to DebtRemaining
            if (debtPayable > buyer._DealerData.DebtRemaining)
            {
                debtPayable = buyer._DealerData.DebtRemaining;
            }
            if (paymentAmount > 0)
            {
                buyer.SendCustomMessage($"You have paid ${paymentAmount} towards your debt.");
            }
            if (buyer._DealerData.DebtRemaining <= 0)
            {
                buyer.SendCustomMessage($"You have no debt remaining after this {source}. Congrats!");
            }
            else if (debtPayable > buyer._DealerData.DebtPaidThisWeek)
            {
                buyer.SendCustomMessage($"You owe us ${buyer._DealerData.DebtRemaining}. You have paid ${buyer._DealerData.DebtPaidThisWeek} this week so far. We will take ${debtPayable - buyer._DealerData.DebtPaidThisWeek} at the beginning of next week or equivalent in products with a bonus multiple of {buyer.Debt.ProductBonus}. If you don't pay, we will take your life.");
            }
            else
            {
                buyer.SendCustomMessage($"You owe us ${buyer._DealerData.DebtRemaining}. You have paid ${buyer._DealerData.DebtPaidThisWeek} this week so far. Congrats! This is more than this week's quota. Rest of business this week will be in cash.");
            }

            // Send a message to the player about the debt status
            // This is a placeholder for the actual message sending method
            MelonLogger.Msg($"Debt status for {buyer.DealerName}: {buyer._DealerData.DebtRemaining}");
        }

        // Method to check if debtpayable>debt paid this week, if so set paidthisweek to true
        public void CheckIfPaidThisWeek()
        {
            float debtPayable = buyer.Debt.DayMultiple * (float)Math.Pow(GetNearestWeek(TimeManager.ElapsedDays), buyer.Debt.DayExponent);
            if (debtPayable > buyer._DealerData.DebtPaidThisWeek)
            {
                paidthisweek = false;
            }
            else
            {
                paidthisweek = true;
            }
        }


        private void DebtPayment()
        {
            if (buyer._DealerData.DebtRemaining <= 0)
            {
                TimeManager.OnWeekPass -= DebtPayment; // Unsubscribe if debt is paid off
                return;
            }
            float paymentAmount = CalculateWeeklyPayment();
            buyer._DealerData.DebtRemaining -= paymentAmount; // Deduct the payment amount from the dealer's remaining debt
            if (Money.GetCashBalance() < paymentAmount)
            {
                // If the player doesn't have enough cash, kill the player
                Player.Local.Kill(); // This is a placeholder for the actual kill player method
            }
            Money.ChangeCashBalance(-(int)paymentAmount); // Deduct the payment amount from the player's cash balance
            buyer._DealerData.DebtRemaining = (int)(buyer._DealerData.DebtRemaining * (1 + buyer.Debt.InterestRate)); // Apply interest to the remaining debt
            buyer._DealerData.DebtPaidThisWeek = 0; // Reset the amount paid this week
            //buyer.SendCustomMessage($"You have paid ${paymentAmount} towards your debt.");
            SendDebtMessage((int)paymentAmount,"weekly payment"); // Send the updated debt message
            CheckIfPaidThisWeek();
        }

        private float CalculateWeeklyPayment()
        {
            // Calculate the amount payable based on the DayMultiple, DayExponent and elapsed days as multiple*day**exponent
            float dayMultiple = buyer.Debt.DayMultiple;
            float dayExponent = buyer.Debt.DayExponent;
            float elapsedDays = TimeManager.ElapsedDays;

            float amountPayable = dayMultiple * (float)Math.Pow(elapsedDays, dayExponent);

            amountPayable -= buyer._DealerData.DebtPaidThisWeek;
            if (amountPayable < 0)
            {
                amountPayable = 0; // Ensure the payment is not negative
            }
            // Calculate the weekly payment based on the debt parameters
            //Ensure the amount payable does not exceed the remaining debt
            if (amountPayable > buyer._DealerData.DebtRemaining)
            {
                amountPayable = buyer._DealerData.DebtRemaining;
            }
            //Log the calculation details
            MelonLogger.Msg($"Calculating weekly payment for {buyer.DealerName}: DayMultiple={dayMultiple}, DayExponent={dayExponent}, ElapsedDays={elapsedDays}, InitialAmount={dayMultiple * (float)Math.Pow(elapsedDays, dayExponent)}, AmountPaidThisWeek={buyer._DealerData.DebtPaidThisWeek}, AmountPayable={amountPayable}");
            
            return amountPayable;
        }
    }

}