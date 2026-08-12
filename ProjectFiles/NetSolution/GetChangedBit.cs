#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.UI;
using FTOptix.NativeUI;
using FTOptix.HMIProject;
using FTOptix.Retentivity;
using FTOptix.CoreBase;
using FTOptix.Core;
using FTOptix.NetLogic;
using FTOptix.AuditSigning;
using FTOptix.EventLogger;
using FTOptix.Store;
using FTOptix.RAEtherNetIP;
using FTOptix.CommunicationDriver;
using FTOptix.DataLogger;
using FTOptix.InfluxDBStore;
using FTOptix.InfluxDBStoreLocal;
using FTOptix.SerialPort;

#endregion

public class GetChangedBit : BaseNetLogic
{
    public override void Start()
    {
        // Insert code to be executed when the user-defined logic is started
        string ValueMappingType = Owner.GetVariable("ValueMappingType").Value;

        if (ValueMappingType == "BitValues")
        {
            this.GetBitIndexAndBitValue();
        }
    }

    public override void Stop()
    {
        // Insert code to be executed when the user-defined logic is stopped
    }

    public void GetBitIndexAndBitValue()
    {
        try
        {
            string OldValue1 = Owner.GetVariable("FormattedOldValue").Value;
            string NewValue1 = Owner.GetVariable("FormattedNewValue").Value;

            //Convert string to int
            long OldValue = long.Parse(OldValue1.Replace(",", "").Replace(".", ""), System.Globalization.CultureInfo.InvariantCulture);
            long NewValue = long.Parse(NewValue1.Replace(",", "").Replace(".", ""), System.Globalization.CultureInfo.InvariantCulture);

            //Calculate the absolute value of the differece between NewValue and OldValue
            long DiffValue = Math.Abs(NewValue - OldValue);

            // Get the logarithm of DiffValue as the Bit index
            int BitIndex = (int)Math.Log(DiffValue, 2);
            Owner.GetVariable("BitIndex").Value = BitIndex;

            //Determine whether the symbol bit has changed
            if((OldValue < 0 && NewValue >= 0) || (OldValue >= 0 && NewValue < 0))
            {
                if(NewValue - OldValue > 0)
                {
                    Owner.GetVariable("BitNewValue").Value = 0;
                }
                else
                {
                    Owner.GetVariable("BitNewValue").Value = 1;
                }
            }
            else
            {
                if (NewValue > OldValue)
                {
                    Owner.GetVariable("BitNewValue").Value = 1;
                }
                else
                {
                    Owner.GetVariable("BitNewValue").Value = 0;
                }
            }
        }
        catch
        {
            Log.Error("AuditSign", "OldValue or NewValue are not Integers");
        }
    }
}
