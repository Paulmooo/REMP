export interface UserInfo {
  id: string;
  roles: string[];
  listingCaseIds: number[];
}
export interface LoginResult {
  token: string;
  userName: string;
  email: string;
  roles: string[];
}
export interface Media {
  id: number;
  mediaType: number;
  mediaUrl: string;
  uploadedAt: string;
  isSelected: boolean;
  isHero: boolean;
}
export interface Agent {
  id: string;
  agentFirstName: string;
  agentLastName: string;
  avatarUrl: string;
  companyName: string;
}
// Company candidates include email; Listing AgentBriefDto does not.
export interface CompanyAgent extends Agent {
  email: string;
}
export interface Contact {
  contactId: number;
  firstName: string;
  lastName: string;
  companyName: string;
  profileUrl: string;
  email: string;
  phoneNumber: string;
}
export interface Listing {
  id: number;
  title: string;
  description: string;
  street: string;
  city: string;
  state: string;
  postCode: number;
  longitude: number;
  latitude: number;
  price: number;
  bedrooms: number;
  bathrooms: number;
  garages: number;
  floorArea: number;
  propertyType: number;
  saleCategory: number;
  listingStatus: number;
  agents: Agent[];
  caseContacts: Contact[];
  mediaAssets: Media[];
}
export interface ListingPage {
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  listingCases: Listing[];
}
