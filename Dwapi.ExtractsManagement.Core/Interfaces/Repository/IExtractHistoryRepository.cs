﻿using System;
using Dwapi.ExtractsManagement.Core.Model;
using Dwapi.SharedKernel.Enum;
using Dwapi.SharedKernel.Interfaces;

namespace Dwapi.ExtractsManagement.Core.Interfaces.Repository
{
    public interface IExtractHistoryRepository : IRepository<ExtractHistory,Guid>
    {
        void ClearHistory(Guid extractId);
        ExtractHistory GetLatest(Guid extractId);
        void UpdateStatus(Guid extractId, ExtractStatus status,int? stats=null,string statusInfo="");
    }
}