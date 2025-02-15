﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using Dwapi.ExtractsManagement.Core.Application.Events;
using Dwapi.ExtractsManagement.Core.Interfaces.Loaders.Dwh;
using Dwapi.ExtractsManagement.Core.Interfaces.Repository.Dwh;
using Dwapi.ExtractsManagement.Core.Model.Destination.Dwh;
using Dwapi.ExtractsManagement.Core.Model.Source.Dwh;
using Dwapi.ExtractsManagement.Core.Notifications;
using Dwapi.ExtractsManagement.Core.Profiles;
using Dwapi.SharedKernel.Enum;
using Dwapi.SharedKernel.Events;
using Dwapi.SharedKernel.Model;
using Dwapi.SharedKernel.Utility;
using MediatR;
using Serilog;

namespace Dwapi.ExtractsManagement.Core.Loader.Dwh
{
    public class PatientStatusLoader : IPatientStatusLoader
    {
        private readonly IPatientStatusExtractRepository _patientStatusExtractRepository;
        private readonly ITempPatientStatusExtractRepository _tempPatientStatusExtractRepository;
        private readonly IMediator _mediator;

        public PatientStatusLoader(IPatientStatusExtractRepository patientStatusExtractRepository, ITempPatientStatusExtractRepository tempPatientStatusExtractRepository, IMediator mediator)
        {
            _patientStatusExtractRepository = patientStatusExtractRepository;
            _tempPatientStatusExtractRepository = tempPatientStatusExtractRepository;
            _mediator = mediator;
        }

        public async Task<int> Load(Guid extractId, int found, bool diffSupport)
        {
            int count = 0; var mapper = diffSupport ? ExtractDiffMapper.Instance : ExtractMapper.Instance;

            try
            {
                DomainEvents.Dispatch(
                    new ExtractActivityNotification(extractId, new DwhProgress(
                        nameof(PatientStatusExtract),
                        nameof(ExtractStatus.Loading),
                        found, 0, 0, 0, 0)));


                StringBuilder query = new StringBuilder();
                query.Append($" SELECT s.* FROM {nameof(TempPatientStatusExtract)}s s");
                query.Append($" INNER JOIN PatientExtracts p ON ");
                query.Append($" s.PatientPK = p.PatientPK AND ");
                query.Append($" s.SiteCode = p.SiteCode ");

                const int take = 500;
                var eCount = await  _tempPatientStatusExtractRepository.GetCount(query.ToString());
                var pageCount = _tempPatientStatusExtractRepository.PageCount(take, eCount);

                int page = 1;
                while (page <= pageCount)
                {
                    var tempPatientStatusExtracts =await
                        _tempPatientStatusExtractRepository.ReadAll(query.ToString(), page, take);

                    var batch = tempPatientStatusExtracts.ToList();
                    count += batch.Count;

                    //Auto mapper
                    var extractRecords = mapper.Map<List<TempPatientStatusExtract>, List<PatientStatusExtract>>(batch);
                    foreach (var record in extractRecords)
                    {
                        record.Id = LiveGuid.NewGuid();
                    }
                    //Batch Insert
                    var inserted = _patientStatusExtractRepository.BatchInsert(extractRecords);
                    if (!inserted)
                    {
                        Log.Error($"Extract {nameof(PatientStatusExtract)} not Loaded");
                        return 0;
                    }
                    Log.Debug("saved batch");
                    page++;
                    DomainEvents.Dispatch(
                        new ExtractActivityNotification(extractId, new DwhProgress(
                            nameof(PatientStatusExtract),
                            nameof(ExtractStatus.Loading),
                            found, count , 0, 0, 0)));
                }

                await _mediator.Publish(new DocketExtractLoaded("NDWH", nameof(PatientStatusExtract)));

                return count;

            }
            catch (Exception e)
            {
                Log.Error(e, $"Extract {nameof(PatientStatusExtract)} not Loaded");
                return 0;
            }
        }
    }
}
