pragma solidity ^0.4.24;

contract Owned {
    address public owner;

    constructor() public {
        owner = tx.origin;
    }

    modifier onlyOwner {
        require(msg.sender == owner);
        _;
    }

    function isDeployed() public pure returns (bool) {
        return true;
    }
}

contract SeasonFactory is Owned {
    address[] public seasons;
    address public newVersionAddress;

    event SeasonCreated(uint64 indexed begin, uint64 indexed end, address season);

    function migrateToNewVersion(address newVersionAddress_) public onlyOwner {
        require(newVersionAddress == 0);
        require(newVersionAddress_ != address(this));

        SeasonFactory newVersion = SeasonFactory(newVersionAddress_);
        require(newVersion.owner() == owner);
        require(newVersion.isDeployed());

        newVersionAddress = newVersionAddress_;
    }

    function addSeason(address season) public onlyOwner {
        if (newVersionAddress != 0) {
            SeasonFactory newVersion = SeasonFactory(newVersionAddress);
            newVersion.addSeason(season);
            return;
        }

        SeasonShim seasonShim = SeasonShim(season);
        require(seasonShim.owner() == owner);
        require(seasons.length == 0 || SeasonShim(seasons[seasons.length - 1]).end() < seasonShim.begin());

        seasons.push(seasonShim);
        emit SeasonCreated(seasonShim.begin(), seasonShim.end(), season);
    }

    function getSeasonsCount() public view returns (uint64) {
        if (newVersionAddress != 0) {
            SeasonFactory newVersion = SeasonFactory(newVersionAddress);
            return newVersion.getSeasonsCount();
        }

        return uint64(seasons.length);
    }

    function getSeasonForDate(uint64 date) public view returns (address) {
        if (newVersionAddress != 0) {
            SeasonFactory newVersion = SeasonFactory(newVersionAddress);
            return newVersion.getSeasonForDate(date);
        }

        for (uint64 i = uint64(seasons.length) - 1; i >= 0; i--) {
            SeasonShim season = SeasonShim(seasons[i]);
            if (date >= season.begin() && date <= season.end())
                return season;
        }
        return 0;
    }

    function getLastSeason() public view returns (address) {
        if (newVersionAddress != 0) {
            SeasonFactory newVersion = SeasonFactory(newVersionAddress);
            return newVersion.getLastSeason();
        }

        return seasons[seasons.length - 1];
    }
}

contract SeasonShim is Owned {
    uint64 public begin;
    uint64 public end;

    function getHistoricalIndices() public view returns (uint64[]) {
        return new uint64[](0);
    }

    function getRequestByIndex(uint64) public view returns (bytes30, uint64, Types.DeclarantType, string, uint64, Types.Speciality[], uint64, uint64, string, uint64[], bytes16) {
        return (0, 0, Types.DeclarantType.Individual, "", 0, new Types.Speciality[](0), 0, 0, "", new uint64[](0), 0);
    }

    function getStatusUpdates(bytes30) public view returns (uint64[], uint64[], string) {
        return (new uint64[](0), new uint64[](0), "");
    }
}

contract Season is Owned {
    uint64 public begin;
    uint64 public end;
    string name;

    uint64 requestCount;
    Node[] nodes;
    uint64 headIndex;
    uint64 tailIndex;
    mapping(bytes30 => uint64) requestServiceNumberToIndex;

    event RequestCreated(bytes30 indexed serviceNumber, uint64 index);

    constructor(uint64 begin_, uint64 end_, string name_) public {
        begin = begin_;
        end = end_;
        name = name_;
    }

    function createRequest(bytes30 serviceNumber, uint64 date, Types.DeclarantType declarantType, string declarantName, uint64 fairId, Types.Speciality[] assortment, uint64 district, uint64 region, string details, uint64[] periods, bytes16 userId) public onlyOwner {
        require(getRequestIndex(serviceNumber) < 0, "Request with provided service number already exists");

        nodes.length++;
        uint64 newlyInsertedIndex = getRequestsCount() - 1;

        Request storage request = nodes[newlyInsertedIndex].request;
        request.serviceNumber = serviceNumber;
        request.date = date;
        request.declarantType = declarantType;
        request.declarantName = declarantName;
        request.fairId = fairId;
        request.district = district;
        request.region = region;
        request.assortment = assortment;
        request.details = details;
        request.periods = periods;
        request.userId = userId;
        request.statusUpdates.push(Types.StatusUpdate(date, 1010, ""));
        requestServiceNumberToIndex[request.serviceNumber] = newlyInsertedIndex;

        fixPlacementInHistory(newlyInsertedIndex, date);

        emit RequestCreated(serviceNumber, newlyInsertedIndex);
    }

    function fixPlacementInHistory(uint64 newlyInsertedIndex, uint64 date) private onlyOwner {
        if (newlyInsertedIndex == 0) {
            nodes[0].prev = - 1;
            nodes[0].next = - 1;
            return;
        }

        int index = tailIndex;
        while (index >= 0) {
            Node storage n = nodes[uint64(index)];
            if (n.request.date <= date) {
                break;
            }
            index = n.prev;
        }

        if (index < 0) {
            nodes[headIndex].prev = newlyInsertedIndex;
            nodes[newlyInsertedIndex].next = headIndex;
            nodes[newlyInsertedIndex].prev = - 1;
            headIndex = newlyInsertedIndex;
        }
        else {
            Node storage node = nodes[uint64(index)];
            Node storage newNode = nodes[newlyInsertedIndex];
            newNode.prev = index;
            newNode.next = node.next;
            if (node.next > 0) {
                nodes[uint64(node.next)].prev = newlyInsertedIndex;
            } else {
                tailIndex = newlyInsertedIndex;
            }
            node.next = newlyInsertedIndex;
        }
    }

    function updateStatus(bytes30 serviceNumber, uint64 responseDate, uint64 statusCode, string note) public onlyOwner {
        int index = getRequestIndex(serviceNumber);

        require(index >= 0, "Request with provided service number was not found");

        Request storage request = nodes[uint64(index)].request;
        for (uint64 i = 0; i < request.statusUpdates.length; i++) {
            Types.StatusUpdate storage update = request.statusUpdates[i];
            require(
                update.responseDate != responseDate
                || update.statusCode != statusCode
            || !(bytes(update.note).length == bytes(note).length && containsString(update.note, note))
            );
        }

        request.statusUpdates.push(Types.StatusUpdate(responseDate, statusCode, note));
    }

    function getSeasonDetails() public view returns (uint64, uint64, string) {
        return (begin, end, name);
    }

    function getAllServiceNumbers() public view returns (bytes30[]) {
        bytes30[] memory result = new bytes30[](getRequestsCount());
        for (uint64 i = 0; i < result.length; i++) {
            result[i] = nodes[i].request.serviceNumber;
        }
        return result;
    }

    function getHistoricalIndices() public view returns (uint64[]){
        uint64[] memory result = new uint64[](getRequestsCount());
        uint64 tookCount = 0;
        int currentIndex = headIndex;
        for (uint64 i = 0; i < nodes.length && tookCount < result.length; i++) {
            Node storage node = nodes[uint64(currentIndex)];
            if (isDisitributableStatus(node.request.statusUpdates)) {
                result[tookCount++] = uint64(currentIndex);
            }
            currentIndex = node.next;
        }

        uint64[] memory trimmedResult = new uint64[](tookCount);
        for (uint64 j = 0; j < trimmedResult.length; j++) {
            trimmedResult[j] = result[j];
        }
        return trimmedResult;
    }

    function isDisitributableStatus(Types.StatusUpdate[] statusUpdates) private pure returns (bool) {
        return true;
    }

    function getRequestIndex(bytes30 serviceNumber) public view returns (int) {
        uint64 index = requestServiceNumberToIndex[serviceNumber];

        if (index == 0 && (nodes.length == 0 || nodes[0].request.serviceNumber != serviceNumber)) {
            return - 1;
        }

        return int(index);
    }

    function getRequestByServiceNumber(bytes30 serviceNumber) public view returns (bytes30, uint64, Types.DeclarantType, string, uint64, Types.Speciality[], uint64, uint64, string, uint64[], bytes16) {
        int index = getRequestIndex(serviceNumber);

        if (index < 0) {
            return (0, 0, Types.DeclarantType.Individual, "", 0, new Types.Speciality[](0), 0, 0, "", new uint64[](0), 0);
        }

        return getRequestByIndex(uint64(index));
    }

    function getRequestByIndex(uint64 index) public view returns (bytes30, uint64, Types.DeclarantType, string, uint64, Types.Speciality[], uint64, uint64, string, uint64[], bytes16) {
        Request storage request = nodes[index].request;
        return (request.serviceNumber, request.date, request.declarantType, request.declarantName, request.fairId, request.assortment, request.district, request.region, request.details, request.periods, request.userId);
    }

    function getRequestsCount() public view returns (uint64) {
        return uint64(nodes.length);
    }

    function getStatusUpdates(bytes30 serviceNumber) public view returns (uint64[], uint64[], string) {
        int index = getRequestIndex(serviceNumber);

        if (index < 0) {
            return (new uint64[](0), new uint64[](0), "");
        }

        Request storage request = nodes[uint64(index)].request;
        uint64[] memory dates = new uint64[](request.statusUpdates.length);
        uint64[] memory statusCodes = new uint64[](request.statusUpdates.length);
        string memory notes = "";
        string memory separator = new string(1);
        bytes memory separatorBytes = bytes(separator);
        separatorBytes[0] = 0x1F;
        separator = string(separatorBytes);
        for (uint64 i = 0; i < request.statusUpdates.length; i++) {
            dates[i] = request.statusUpdates[i].responseDate;
            statusCodes[i] = request.statusUpdates[i].statusCode;
            notes = strConcat(notes, separator, request.statusUpdates[i].note);
        }

        return (dates, statusCodes, notes);
    }

    function getMatchingRequests(uint64 skipCount, uint64 takeCount, Types.DeclarantType[] declarantTypes, string declarantName, uint64 fairId, Types.Speciality[] assortment, uint64 district) public view returns (uint64[]) {
        uint64[] memory result = new uint64[](takeCount);
        uint64 skippedCount = 0;
        uint64 tookCount = 0;
        int currentIndex = headIndex;
        for (uint64 i = 0; i < nodes.length && tookCount < result.length; i++) {
            Node storage node = nodes[uint64(currentIndex)];
            if (isMatch(node.request, declarantTypes, declarantName, fairId, assortment, district)) {
                if (skippedCount < skipCount) {
                    skippedCount++;
                }
                else {
                    result[tookCount++] = uint64(currentIndex);
                }
            }
            currentIndex = node.next;
        }

        uint64[] memory trimmedResult = new uint64[](tookCount);
        for (uint64 j = 0; j < trimmedResult.length; j++) {
            trimmedResult[j] = result[j];
        }
        return trimmedResult;
    }

    function isMatch(Request request, Types.DeclarantType[] declarantTypes, string declarantName_, uint64 fairId_, Types.Speciality[] assortment_, uint64 district_) private pure returns (bool) {
        if (declarantTypes.length != 0 && !containsDeclarant(declarantTypes, request.declarantType)) {
            return false;
        }
        if (!isEmpty(declarantName_) && !containsString(request.declarantName, declarantName_)) {
            return false;
        }
        if (fairId_ != 0 && fairId_ != request.fairId) {
            return false;
        }
        if (district_ != 0 && district_ != request.district) {
            return false;
        }
        if (assortment_.length > 0) {
            for (uint64 i = 0; i < assortment_.length; i++) {
                if (contains(request.assortment, assortment_[i])) {
                    return true;
                }
            }
            return false;
        }
        return true;
    }

    function contains(Types.Speciality[] array, Types.Speciality value) private pure returns (bool) {
        for (uint i = 0; i < array.length; i++) {
            if (array[i] == value)
                return true;
        }
        return false;
    }

    function containsDeclarant(Types.DeclarantType[] array, Types.DeclarantType value) private pure returns (bool) {
        for (uint i = 0; i < array.length; i++) {
            if (array[i] == value)
                return true;
        }
        return false;
    }

    function isEmpty(string value) private pure returns (bool) {
        return bytes(value).length == 0;
    }

    function containsString(string _base, string _value) internal pure returns (bool) {
        bytes memory _baseBytes = bytes(_base);
        bytes memory _valueBytes = bytes(_value);

        if (_baseBytes.length < _valueBytes.length) {
            return false;
        }

        for (uint j = 0; j <= _baseBytes.length - _valueBytes.length; j++) {
            uint i = 0;
            for (; i < _valueBytes.length; i++) {
                if (_baseBytes[i + j] != _valueBytes[i]) {
                    break;
                }
            }

            if (i == _valueBytes.length)
                return true;
        }

        return false;
    }

    function strConcat(string _a, string _b, string _c) private pure returns (string){
        bytes memory _ba = bytes(_a);
        bytes memory _bb = bytes(_b);
        bytes memory _bc = bytes(_c);
        string memory abcde = new string(_ba.length + _bb.length + _bc.length);
        bytes memory babcde = bytes(abcde);
        uint k = 0;
        for (uint i = 0; i < _ba.length; i++) babcde[k++] = _ba[i];
        for (i = 0; i < _bb.length; i++) babcde[k++] = _bb[i];
        for (i = 0; i < _bc.length; i++) babcde[k++] = _bc[i];
        return string(babcde);
    }

    struct Node {
        Request request;
        int prev;
        int next;
    }

    struct Request {
        bytes30 serviceNumber;
        uint64 date;
        Types.DeclarantType declarantType;
        string declarantName;
        uint64 fairId;
        Types.Speciality[] assortment;
        uint64[] periods;
        uint64 district; // ÓÍÛ„
        uint64 region; // ‡ÈÓÌ
        Types.StatusUpdate[] statusUpdates;
        string details;
        bytes16 userId;
    }
}

contract Distributor is Owned {
    address public seasonAddress;
    uint64 public date;
    uint64 public tradeSessionId;
    bool public isLoaded;
    bool public isCompleted;
    DistributorRequest[] invididualsRequests;
    DistributorRequest[] farmerRequests;
    DistributorRequest[] ieLeRequests;
    FairPeriod[] allPeriods;
    mapping(uint256 => mapping(uint256 => mapping(uint256 => uint))) fairsToPeriodsToSpecialitiesToPeriodIndex;
    mapping(uint256 => bool) isDeclarantRegisteredForPeriod;

    constructor(address seasonAddress_, uint64 date_, uint64 tradeSessionId_, uint64[] fairsIds, uint64[] periods, uint64[] specialities, uint64[] individualsPlacesCount, uint64[] farmersPlacesCount, uint64[] ieLesPlacesCounts) public {
        SeasonShim seasonShim = SeasonShim(seasonAddress_);
        require(seasonShim.owner() == owner);

        seasonAddress = seasonAddress_;
        date = date_;
        tradeSessionId = tradeSessionId_;
        for (uint i = 0; i < fairsIds.length; i++) {
            FairPeriod memory fairPeriod;
            fairPeriod.date = periods[i];
            fairPeriod.ieLesPlacesCount = ieLesPlacesCounts[i];
            fairPeriod.individualsPlacesCount = individualsPlacesCount[i];
            fairPeriod.farmersPlacesCount = farmersPlacesCount[i];
            allPeriods.push(fairPeriod);
            fairsToPeriodsToSpecialitiesToPeriodIndex[fairsIds[i]][periods[i]][specialities[i]] = i;
        }
    }

    function loadRequests() public onlyOwner {
        require(!isLoaded);

        SeasonShim season = SeasonShim(seasonAddress);
        uint64[] memory indices = season.getHistoricalIndices();
        for (uint i = 0; i < indices.length; i++) {
            DistributorRequest memory request = getRequest(season, indices[i]);
            if (request.declarantType == Types.DeclarantType.Individual || request.declarantType == Types.DeclarantType.IndividualAsIndividualEntrepreneur) {
                invididualsRequests.push(request);
            }
            else if (request.declarantType == Types.DeclarantType.Farmer) {
                farmerRequests.push(request);
            }
            else {
                ieLeRequests.push(request);
            }
        }

        isLoaded = true;
    }

    function getRequest(SeasonShim season, uint64 index) private view returns (DistributorRequest) {
        var (serviceNumber, requestDate, declarantType, declarantName, fairId, assortment, district, region, details, periods, userId) = season.getRequestByIndex(index);
        return DistributorRequest(serviceNumber, userId, declarantType, fairId, periods, assortment);
    }
    
    function getTotalRequestsCount() public view returns (uint64) {
        return uint64(invididualsRequests.length + farmerRequests.length + ieLeRequests.length);
    }

    function distribute() public {
        require(isLoaded);
        require(!isCompleted);

        // 1
        distributeIndividuals();

        // 2
        distributeFarmers(true); // 2.1-2.6
        distributeFarmers(false); // 2.7

        // 3
        distributeIeLe(true);
        distributeIeLe(false);

        // 4
        for(uint i = 0; ; i++ ) {
            RedistributionResult result = redistribute();
            if (result == RedistributionResult.AllPeriodsAreSet) {
                break;
            }
            require(i < 3, "Infinite loop like IE -> PE -> Farmers -> IE has been detected");
        }

        isCompleted = true;
    }

    function distributeIndividuals() private {
        for (uint i = 0; i < invididualsRequests.length; i++) {
            DistributorRequest storage request = invididualsRequests[i]; // 1.1

            for (uint j = 0; j < request.periods.length; i++) {
                for (uint k = 0; k < request.assortment.length; k++) {
                    if (request.assortment[i] != Types.Speciality.Vegetables) {
                        continue;
                    }
                    
                    uint256 periodId = (uint256(request.periods[i]) << 128) | uint256(request.userId); // it's basically a tuple (period, userId)
                    if (isDeclarantRegisteredForPeriod[periodId]) {
                        continue; // skip registered PEs
                    }

                    FairPeriod storage period = allPeriods[fairsToPeriodsToSpecialitiesToPeriodIndex[request.fairId][request.periods[i]][uint256(request.assortment[k])]];
                    if (period.individuals.length < period.individualsPlacesCount) {
                        period.individuals.push(request.userId); // 1.2
                        isDeclarantRegisteredForPeriod[periodId] = true; // 1.3 setting mark that other requests for this declarant should be declined
                        period.isRequestApproved[request.serviceNumber] = true;  // 1.3 setting mark that request was processed
                    }
                }
            }
        }
    }

    function distributeFarmers(bool shouldDeclineRequestsWithLoweredPriority) private {
        for (uint i = 0; i < farmerRequests.length; i++) {
            DistributorRequest storage request = farmerRequests[i]; // 2.1

            for (uint j = 0; j < request.periods.length; i++) {
                for (uint k = 0; k < request.assortment.length; k++) {
                    if (request.assortment[i] != Types.Speciality.Vegetables) {
                        continue; 
                    }
                    
                    uint256 periodId = (uint256(request.periods[i]) << 128) | uint256(request.userId); // it's basically a tuple (period, userId)
                    if (shouldDeclineRequestsWithLoweredPriority && isDeclarantRegisteredForPeriod[periodId]) {
                        continue; // skip request with lowered priority
                    }

                    FairPeriod storage period = allPeriods[fairsToPeriodsToSpecialitiesToPeriodIndex[request.fairId][request.periods[i]][uint256(request.assortment[k])]];
                    if (period.isRequestApproved[request.serviceNumber]) {
                        continue; // skipping already processed requsets
                    }

                    if (period.farmers.length < period.farmersPlacesCount) {
                        period.farmers.push(request.userId); // 2.2
                        isDeclarantRegisteredForPeriod[periodId] = true; // 2.3 lowering priority
                        period.isRequestApproved[request.serviceNumber] = true; // 2.3 setting mark that request was processed
                    }
                }
            }
        }
    }

    function distributeIeLe(bool shouldDeclineRequestsWithLoweredPriority) private {
        for (uint i = 0; i < ieLeRequests.length; i++) {
            DistributorRequest storage request = ieLeRequests[i]; // 3.1
            
            for (uint j = 0; j < request.periods.length; i++) {
                for (uint k = 0; k < request.assortment.length; k++) {
                    uint256 periodId = (uint256(request.periods[i]) << 128) | uint256(request.userId); // it's basically a tuple (period, userId)
                    if (shouldDeclineRequestsWithLoweredPriority && isDeclarantRegisteredForPeriod[periodId]) {
                        continue; // skip request with lowered priority
                    }

                    FairPeriod storage period = allPeriods[fairsToPeriodsToSpecialitiesToPeriodIndex[request.fairId][request.periods[i]][uint256(request.assortment[k])]];
                    if (period.isRequestApproved[request.serviceNumber]) {
                        continue; // skipping already processed requsets
                    }
                    if (period.ieLes.length < period.ieLesPlacesCount) {
                        period.ieLes.push(request.userId); // 3.2
                        isDeclarantRegisteredForPeriod[periodId] = true; // 3.3 lowering priority
                        period.isRequestApproved[request.serviceNumber] = true; // 3.3 setting mark that request was processed
                    }
                }
            }
        }
    }

    mapping (uint64 => RedistributionInfo) periodInfo;

    function redistribute() private returns (RedistributionResult) {
        RedistributionResult result = RedistributionResult.AllPeriodsAreSet;
        for (uint i = 0; i < allPeriods.length; i++) {
            FairPeriod storage period = allPeriods[i];
            if (period.ieLes.length < period.ieLesPlacesCount) { // 4.1
                uint64 ieLesDiff = uint64(period.ieLesPlacesCount - period.ieLes.length);
                period.ieLesPlacesCount = uint64(period.ieLes.length);

                if (!periodInfo[period.date].wasRedistributedToPEs) {
                    period.individualsPlacesCount += ieLesDiff;
                    result = RedistributionResult.NeedDistributionRerun;
                    periodInfo[period.date].wasRedistributedToPEs = true;
                }
                else if (!periodInfo[period.date].wasRedistributedToFarmers) {
                    period.farmersPlacesCount += ieLesDiff;
                    result = RedistributionResult.NeedDistributionRerun;
                    periodInfo[period.date].wasRedistributedToFarmers = true;                    
                }
            }
            else if (period.individuals.length < period.individualsPlacesCount) { // 4.2
                uint64 peDiff = uint64(period.individualsPlacesCount - period.individuals.length);
                period.individualsPlacesCount = uint64(period.individuals.length);

                if (!periodInfo[period.date].wasRedistributedToFarmers) {
                    period.farmersPlacesCount += peDiff;
                    result = RedistributionResult.NeedDistributionRerun;
                    periodInfo[period.date].wasRedistributedToFarmers = true;                    
                }
                else if (!periodInfo[period.date].wasRedistributedToIELEs) {
                    period.ieLesPlacesCount += peDiff;
                    result = RedistributionResult.NeedDistributionRerun;
                    periodInfo[period.date].wasRedistributedToIELEs = true;                    
                }
            }
            else if (period.farmers.length < period.farmersPlacesCount) { // 4.3
                uint64 farmerDiff = uint64(period.farmersPlacesCount - period.farmers.length);
                period.individualsPlacesCount = uint64(period.farmers.length);

                if (!periodInfo[period.date].wasRedistributedToPEs) {
                    period.individualsPlacesCount += farmerDiff;
                    result = RedistributionResult.NeedDistributionRerun;
                    periodInfo[period.date].wasRedistributedToPEs = true;
                }
                else if (!periodInfo[period.date].wasRedistributedToIELEs) {
                    period.ieLesPlacesCount += farmerDiff;
                    result = RedistributionResult.NeedDistributionRerun;
                    periodInfo[period.date].wasRedistributedToIELEs = true;                    
                }
            }
        }
    }

    struct FairPeriod {
        uint64 date;
        uint64 ieLesPlacesCount;
        uint64 individualsPlacesCount;
        uint64 farmersPlacesCount;
        bytes16[] individuals;
        bytes16[] farmers;
        bytes16[] ieLes;
        
        mapping(bytes30 => bool) isRequestApproved;
    }

    struct DistributorRequest {
        bytes30 serviceNumber;
        bytes16 userId;
        Types.DeclarantType declarantType;
        uint64 fairId;
        uint64[] periods;
        Types.Speciality[] assortment;
    }

    enum RedistributionResult {
        AllPeriodsAreSet,
        NeedDistributionRerun
    }

    struct RedistributionInfo {
        bool wasRedistributedToPEs;
        bool wasRedistributedToFarmers;
        bool wasRedistributedToIELEs;
    }
}

library Types {
    struct StatusUpdate {
        uint64 responseDate;
        uint64 statusCode;
        string note;
    }

    enum DeclarantType {
        Individual, // ‘À
        IndividualEntrepreneur, // »œ
        LegalEntity, // ﬁÀ
        IndividualAsIndividualEntrepreneur, // ‘À Í‡Í ﬁÀ
        Farmer // »œ  ‘’
    }

    enum Speciality {
        Unused, // solidity doesn't allow to start enums from 1. Don't use it, it's not actual a Types.Speciality
        Vegetables,
        Meat,
        Fish,
        FoodStuffs
    }
}
