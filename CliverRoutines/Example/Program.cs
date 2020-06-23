﻿using System;
using System.Threading;
using Cliver;

namespace Example
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Log.Initialize(Log.Mode.EACH_SESSION_IS_IN_OWN_FORLDER);

                Log.Inform("test");
                ThreadRoutines.StartTry(() =>
                {
                    Log.Inform0("to default log");
                    Log.Thread.Inform0("to thread log");
                    throw new Exception2("test exception2");
                },
                (Exception e) =>
                {
                    Log.Thread.Error(e);
                }
                );

                Log.Session s1 = Log.Session.Get("Name1");//open if not open session "Name1"
                Log.Writer nl = s1["Name"];//open if not open log "Name"
                nl.Error("to log 'Name'");
                s1.Trace("to the main log of session 'Name1'");
                s1.Thread.Inform("to the thread log of session 'Name1'");
                s1.Rename("Name2");


                Config.Reload();

                //the usual routine is direct manipulating with settings data
                Settings.Smtp.Port = 10;
                Settings.Smtp.Save();

                //a more advanced routine which is usually not required
                SmtpSettings smtpSettings2 = Config.CreateResetCopy(Settings.Smtp);
                //pass smtpSettings2 somewhere for editing...
                smtpSettings2.Port = 10;
                Settings.Smtp = smtpSettings2;
                Settings.Smtp.Save();
            }
            catch(Exception e)
            {
                Log.Exit(e);
            }            
        }
    }
}
