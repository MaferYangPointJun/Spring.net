/*
 * Copyright 2002-2010 the original author or authors.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */


using System;
using System.Collections;

using Common.Logging;

using Quartz;
using Quartz.Xml;

using Spring.Collections;
using Spring.Context;
using Spring.Core.IO;
using Spring.Transaction;
using Spring.Transaction.Support;

namespace Spring.Scheduling.Quartz
{
    /// <summary>
    /// Common base class for accessing a Quartz Scheduler, i.e. for registering jobs,
    /// triggers and listeners on a <see cref="IScheduler" /> instance.
    /// </summary>
    /// <remarks>
    /// For concrete usage, check out the <see cref="SchedulerFactoryObject" /> and
    /// <see cref="SchedulerAccessorObject" /> classes.
    ///</remarks>
    /// <author>Juergen Hoeller</author>
    /// <author>Marko Lahma (.NET)</author>
    public abstract class SchedulerAccessor : IResourceLoaderAware
    {
        /// <summary>
        /// Logger instance.
        /// </summary>
        protected readonly ILog logger;
        private bool overwriteExistingJobs;

        private string[] jobSchedulingDataLocations;

        private IList jobDetails;
        private IDictionary calendars;
        private IList triggers;

        private ISchedulerListener[] schedulerListeners;
        private IJobListener[] globalJobListeners;
        private IJobListener[] jobListeners;
        private ITriggerListener[] globalTriggerListeners;
        private ITriggerListener[] triggerListeners;

        private IPlatformTransactionManager transactionManager;
        /// <summary>
        /// Resource loader instance for sub-classes
        /// </summary>
        protected IResourceLoader resourceLoader;

        /// <summary>
        /// Initializes a new instance of the <see cref="SchedulerAccessor"/> class.
        /// </summary>
        protected SchedulerAccessor()
        {
            logger = LogManager.GetLogger(GetType());
        }

        /// <summary>
        /// Set whether any jobs defined on this SchedulerFactoryObject should overwrite
        /// existing job definitions. Default is "false", to not overwrite already
        /// registered jobs that have been read in from a persistent job store.
        /// </summary>
        public virtual bool OverwriteExistingJobs
        {
            set { overwriteExistingJobs = value; }
        }


        /// <summary>
        /// Set the locations of Quartz job definition XML files that follow the
        /// "job_scheduling_data_1_5" XSD. Can be specified to automatically
        /// register jobs that are defined in such files, possibly in addition
        /// to jobs defined directly on this SchedulerFactoryObject.
        /// </summary>
        /// <seealso cref="JobSchedulingDataProcessor" />
        public virtual string[] JobSchedulingDataLocations
        {
            set { jobSchedulingDataLocations = value; }
        }

        /// <summary> 
        /// Set the location of a Quartz job definition XML file that follows the
        /// "job_scheduling_data" XSD. Can be specified to automatically
        /// register jobs that are defined in such a file, possibly in addition
        /// to jobs defined directly on this SchedulerFactoryObject.
        /// </summary>
        /// <seealso cref="ResourceJobSchedulingDataProcessor" />
        /// <seealso cref="JobSchedulingDataProcessor" />
        public virtual string JobSchedulingDataLocation
        {
            set { jobSchedulingDataLocations = new string[] {value}; }
        }

        /// <summary>
        /// Register a list of JobDetail objects with the Scheduler that
        /// this FactoryObject creates, to be referenced by Triggers.
        /// This is not necessary when a Trigger determines the JobDetail
        /// itself: In this case, the JobDetail will be implicitly registered
        /// in combination with the Trigger.
        /// </summary>
        /// <seealso cref="Triggers" />
        /// <seealso cref="JobDetail" />
        /// <seealso cref="JobDetailObject" />
        /// <seealso cref="IJobDetailAwareTrigger" />
        /// <seealso cref="Trigger.JobName" />
        public virtual JobDetail[] JobDetails
        {
            set
            {
                // Use modifiable ArrayList here, to allow for further adding of
                // JobDetail objects during autodetection of JobDetailAwareTriggers.
                jobDetails = new ArrayList(value);
            }
        }

        /// <summary>
        /// Register a list of Quartz ICalendar objects with the Scheduler
        /// that this FactoryObject creates, to be referenced by Triggers.
        /// </summary>
        /// <value>Map with calendar names as keys as Calendar objects as values</value> 
        /// <seealso cref="ICalendar" />
        /// <seealso cref="Trigger.CalendarName" />
        public virtual IDictionary Calendars
        {
            set { calendars = value; }
        }

        /// <summary>
        /// Register a list of Trigger objects with the Scheduler that
        /// this FactoryObject creates.
        /// </summary>
        /// <remarks>
        /// If the Trigger determines the corresponding JobDetail itself,
        /// the job will be automatically registered with the Scheduler.
        /// Else, the respective JobDetail needs to be registered via the
        /// "jobDetails" property of this FactoryObject.
        /// </remarks>
        /// <seealso cref="JobDetails" />
        /// <seealso cref="JobDetail" />
        /// <seealso cref="IJobDetailAwareTrigger" />
        /// <seealso cref="CronTriggerObject" />
        /// <seealso cref="SimpleTriggerObject" />
        public virtual Trigger[] Triggers
        {
            set { triggers = new ArrayList(value); }
        }

        /// <summary>
        /// Specify Quartz SchedulerListeners to be registered with the Scheduler.
        /// </summary>
        public virtual ISchedulerListener[] SchedulerListeners
        {
            set { schedulerListeners = value; }
        }

        /// <summary>
        /// Specify global Quartz JobListeners to be registered with the Scheduler.
        /// Such JobListeners will apply to all Jobs in the Scheduler.
        /// </summary>
        public virtual IJobListener[] GlobalJobListeners
        {
            set { globalJobListeners = value; }
        }

        /// <summary>
        /// Specify named Quartz JobListeners to be registered with the Scheduler.
        /// Such JobListeners will only apply to Jobs that explicitly activate
        /// them via their name.
        /// </summary>
        /// <seealso cref="IJobListener.Name" />
        /// <seealso cref="JobDetail.AddJobListener" />
        /// <seealso cref="JobDetail.JobListenerNames" />
        public virtual IJobListener[] JobListeners
        {
            set { jobListeners = value; }
        }


        /// <summary>
        /// Specify global Quartz TriggerListeners to be registered with the Scheduler.
        /// Such TriggerListeners will apply to all Triggers in the Scheduler.
        /// </summary>
        public virtual ITriggerListener[] GlobalTriggerListeners
        {
            set { globalTriggerListeners = value; }
        }


        /// <summary>
        /// Specify named Quartz TriggerListeners to be registered with the Scheduler.
        /// Such TriggerListeners will only apply to Triggers that explicitly activate
        /// them via their name.
        /// </summary>
        /// <seealso cref="ITriggerListener.Name" />
        /// <seealso cref="Trigger.AddTriggerListener" />
        /// <seealso cref="Trigger.TriggerListenerNames" />
        public virtual ITriggerListener[] TriggerListeners
        {
            set { triggerListeners = value; }
        }


        /// <summary>
        /// Set the transaction manager to be used for registering jobs and triggers
        /// that are defined by this SchedulerFactoryObject. Default is none; setting
        /// this only makes sense when specifying a DataSource for the Scheduler.
        /// </summary>
        public virtual IPlatformTransactionManager TransactionManager
        {
            set { transactionManager = value; }
        }

        /// <summary>
        /// Sets the <see cref="Spring.Core.IO.IResourceLoader"/>
        /// that this object runs in.
        /// </summary>
        /// <value></value>
        /// <remarks>
        /// Invoked <b>after</b> population of normal objects properties but
        /// before an init callback such as
        /// <see cref="Spring.Objects.Factory.IInitializingObject"/>'s
        /// <see cref="Spring.Objects.Factory.IInitializingObject.AfterPropertiesSet()"/>
        /// or a custom init-method. Invoked <b>before</b> setting
        /// <see cref="Spring.Context.IApplicationContextAware"/>'s
        /// <see cref="Spring.Context.IApplicationContextAware.ApplicationContext"/>
        /// property.
        /// </remarks>
        public virtual IResourceLoader ResourceLoader
        {
            set { resourceLoader = value; }
        }

        /// <summary>
        /// Register jobs and triggers (within a transaction, if possible).
        /// </summary>
        protected virtual void RegisterJobsAndTriggers()
        {
            ITransactionStatus transactionStatus = null;
            if (transactionManager != null)
            {
                transactionStatus = transactionManager.GetTransaction(new DefaultTransactionDefinition());
            }
            try
            {
                if (jobSchedulingDataLocations != null)
                {
                    JobSchedulingDataProcessor dataProcessor = new JobSchedulingDataProcessor(true, true);
                    for (int i = 0; i < this.jobSchedulingDataLocations.Length; i++)
                    {
                        dataProcessor.ProcessFileAndScheduleJobs(
                            jobSchedulingDataLocations[i], GetScheduler(), overwriteExistingJobs);
                    }
                }

                // Register JobDetails.
                if (jobDetails != null)
                {
                    foreach (JobDetail jobDetail in jobDetails)
                    {
                        AddJobToScheduler(jobDetail);
                    }
                }
                else
                {
                    // Create empty list for easier checks when registering triggers.
                    jobDetails = new LinkedList();
                }

                // Register Calendars.
                if (calendars != null)
                {
                    foreach (DictionaryEntry entry in calendars)
                    {
                        string calendarName = (string) entry.Key;
                        ICalendar calendar = (ICalendar) entry.Value;
                        GetScheduler().AddCalendar(calendarName, calendar, true, true);
                    }
                }

                // Register Triggers.
                if (triggers != null)
                {
                    foreach (Trigger trigger in triggers)
                    {
                        AddTriggerToScheduler(trigger);
                    }
                }
            }

            catch (Exception ex)
            {
                if (transactionStatus != null)
                {
                    try
                    {
                        transactionManager.Rollback(transactionStatus);
                    }
                    catch (TransactionException)
                    {
                        logger.Error("Job registration exception overridden by rollback exception", ex);
                        throw;
                    }
                }
                if (ex is SchedulerException)
                {
                    throw;
                }
                throw new SchedulerException("Registration of jobs and triggers failed: " + ex.Message);
            }

            if (transactionStatus != null)
            {
                transactionManager.Commit(transactionStatus);
            }
        }

        /// <summary>
        /// Add the given job to the Scheduler, if it doesn't already exist.
        /// Overwrites the job in any case if "overwriteExistingJobs" is set.
        /// </summary>
        /// <param name="jobDetail">the job to add</param>
        /// <returns><code>true</code> if the job was actually added, <code>false</code> if it already existed before</returns>
        private bool AddJobToScheduler(JobDetail jobDetail)
        {
            if (overwriteExistingJobs ||
                GetScheduler().GetJobDetail(jobDetail.Name, jobDetail.Group) == null)
            {
                GetScheduler().AddJob(jobDetail, true);
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Add the given trigger to the Scheduler, if it doesn't already exist.
        /// Overwrites the trigger in any case if "overwriteExistingJobs" is set.
        /// </summary>
        /// <param name="trigger">the trigger to add</param>
        /// <returns><code>true</code> if the trigger was actually added, <code>false</code> if it already existed before</returns>
        private bool AddTriggerToScheduler(Trigger trigger)
        {
            bool triggerExists = (GetScheduler().GetTrigger(trigger.Name, trigger.Group) != null);
            if (!triggerExists || this.overwriteExistingJobs)
            {
                // Check if the Trigger is aware of an associated JobDetail.
                if (trigger is IJobDetailAwareTrigger)
                {
                    JobDetail jobDetail = ((IJobDetailAwareTrigger) trigger).JobDetail;
                    // Automatically register the JobDetail too.
                    if (!jobDetails.Contains(jobDetail) && AddJobToScheduler(jobDetail))
                    {
                        jobDetails.Add(jobDetail);
                    }
                }
                if (!triggerExists)
                {
                    try
                    {
                        GetScheduler().ScheduleJob(trigger);
                    }
                    catch (ObjectAlreadyExistsException ex)
                    {
                        if (logger.IsDebugEnabled)
                        {
                            logger.Debug(
                                "Unexpectedly found existing trigger, assumably due to cluster race condition: " +
                                ex.Message + " - can safely be ignored");
                        }
                        if (overwriteExistingJobs)
                        {
                            GetScheduler().RescheduleJob(trigger.Name, trigger.Group, trigger);
                        }
                    }
                }
                else
                {
                    GetScheduler().RescheduleJob(trigger.Name, trigger.Group, trigger);
                }
                return true;
            }
            else
            {
                return false;
            }
        }


        /// <summary>
        /// Register all specified listeners with the Scheduler.
        /// </summary>
        protected virtual void RegisterListeners()
        {
            if (schedulerListeners != null)
            {
                for (int i = 0; i < schedulerListeners.Length; i++)
                {
                    GetScheduler().AddSchedulerListener(schedulerListeners[i]);
                }
            }
            if (globalJobListeners != null)
            {
                for (int i = 0; i < globalJobListeners.Length; i++)
                {
                    GetScheduler().AddGlobalJobListener(globalJobListeners[i]);
                }
            }
            if (jobListeners != null)
            {
                for (int i = 0; i < jobListeners.Length; i++)
                {
                    GetScheduler().AddJobListener(jobListeners[i]);
                }
            }
            if (globalTriggerListeners != null)
            {
                for (int i = 0; i < globalTriggerListeners.Length; i++)
                {
                    GetScheduler().AddGlobalTriggerListener(globalTriggerListeners[i]);
                }
            }
            if (triggerListeners != null)
            {
                for (int i = 0; i < triggerListeners.Length; i++)
                {
                    GetScheduler().AddTriggerListener(triggerListeners[i]);
                }
            }
        }

        /// <summary>
        /// Template method that determines the Scheduler to operate on.
        /// To be implemented by subclasses.
        /// </summary>
        /// <returns></returns>
        protected abstract IScheduler GetScheduler();
    }
}