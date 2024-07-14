import { NetSectionItem } from "./NetSectionItem";

export interface RoadProperties {
    Name: string;
    SpeedLimit: number;
    GeneratesTrafficLights: boolean;
    GeneratesZoningBlocks: boolean;
    MaxSlopeSteepness: number; // ignore
    AggregateType: string; // ignore
    Category: RoadCategory;
}

export interface RoadLane {
    SectionPrefabName: string;
    Index: number;
    Invert?: boolean;
	  IsGroup: boolean;
    NetSection?: NetSectionItem;
    Options?: OptionSection[];
}

export interface OptionSection {
    id: number;
    name: string;
    options: OptionItem[];
  }
  
  export interface OptionItem {
    id: number;
    name: string;
    icon: string;
    selected: boolean;
    isValue: boolean;
    disabled: boolean;
    value: string;
  }
  

// Flag Enum
export enum RoadCategory {
    Road = 0,
    Highway = 1,
    PublicTransport = 2,
}