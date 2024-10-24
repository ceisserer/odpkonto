using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
//using Dapper;
using Oracle.ManagedDataAccess.Client;

namespace ConsoleApplication5
{
    public static class RandomGenThrSafe
    {
        private static Random _inst = new Random();

        public static int Next(int limit)
        {
            lock (_inst) return _inst.Next(limit);
        }
    }

    class Program
    {
        static int CalculateNewBalance(int kontoBalance, int amount)
        {
            //Expensive computation
            Thread.Sleep(RandomGenThrSafe.Next(25));
            return kontoBalance - amount;
        }

        // Führt Überweisung zwischen zwei Konten durch.
        // source: PK des Quellkontos, dest: PK des Zielkontos
        static bool TransferMoney(OracleConnection oc, int source, int dest, int amount)
        {
            // Transaktion starten
            OracleTransaction txn = oc.BeginTransaction();
            try
            {
                OracleCommand cmd = oc.CreateCommand();

                // Kontostand des Quellkontos lesen
                int sourceBalance = -1;
                cmd.CommandText = "SELECT balance FROM konto WHERE kid=" + source;
                OracleDataReader reader = cmd.ExecuteReader();
                if (reader.Read())
                {   // Query mit einer Ergebniszeile, wenn es das Quellkonto nicht gibt -> Felher werfen
                    sourceBalance = reader.GetInt32(0);
                }
                else
                {
                    throw new Exception("invalid source konto specified: " + source);
                }
                reader.Close();

                // Ein Konto darf nicht negativ werden
                if (sourceBalance < 0)
                {
                    throw new Exception("this should not happen!!!");
                }

                // Neuen Kontostand des Quellkontos berechnen.
                // Berechnung wird durch C# durchgeführt, könnte theoretisch komplex sein (Gebühren, ...)
                int newBalance = CalculateNewBalance(sourceBalance, amount);

                // Sofern die Überweisung nicht zu einem negativen Kontostand führen würde
                // Quell und Zielkonto mit aktualisierten Werten updaten.
                if (newBalance > 0)
                {
                    cmd.CommandText = "UPDATE konto SET balance = " + newBalance + " WHERE kid=" + source;
                    int modifiedRows = cmd.ExecuteNonQuery(); // Anzahl der veränderten Zeilen
                    cmd.CommandText = "UPDATE konto SET balance = balance + " + amount + " WHERE kid=" + dest;
                    cmd.ExecuteNonQuery();
                    txn.Commit();
                    return true; // Überweisung war erfolgreich
                }

                txn.Rollback();
            }
            catch (Exception e)
            {
                txn.Rollback();
                Console.WriteLine(e.Message);
            }

            return false;
        }

        //Führt 1000 Überweisungen zwischen zufälligen Konten aus    
        static void TestTransferMoneyRandom(OracleConnection oc)
        {
            for (int i = 0; i < 1000; i++)
            {
                int source = RandomGenThrSafe.Next(4);
                int dest = RandomGenThrSafe.Next(4);
                int difference = RandomGenThrSafe.Next(1000000);
                TransferMoney(oc, source, dest, difference);
            }
        }

        //Führt 1000 Überweisungen von King -> Herbert aus
        static void TestTransferMoneyFromKingToHerbert(OracleConnection oc)
        {
            for (int i = 0; i < 1000; i++)
            {
                TransferMoney(oc, 0, 3, 1);
            }
        }

        static void ExecuteTests(object thrIdx)
        {
            string connectString = "Data Source=(DESCRIPTION=(ADDRESS_LIST=(ADDRESS=(PROTOCOL=TCP)(HOST=localhost)(PORT=1521)))(CONNECT_DATA=(SERVER=DEDICATED)(SERVICE_NAME=FREEPDB1)));User Id=system;Password=dbi2324;";

            try
            {
                using (OracleConnection oc = new OracleConnection(connectString))
                {
                    oc.Open();

                    // Uncomment the test you would like to execute below
                    // TestTransferMoneyFromKingToHerbert(oc);
                    // TestTransferMoneyRandom(oc);

                    oc.Close();
                }
            }
            catch (OracleException e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }

        static void Main(string[] args)
        {
            // Anzahl der gleichzeitigen Aufrufe kann hier eingestellt werden
            const int numberOfParallelThreads = 1;

            Thread[] threads = new Thread[numberOfParallelThreads];
            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(ExecuteTests);
                threads[i].Start(i);
            }

            // Wait for all threads to finish
            for (int i = 0; i < threads.Length; i++)
            {
                threads[i].Join();
            }

            Console.WriteLine("All threads have finished.");
        }
    }
}
