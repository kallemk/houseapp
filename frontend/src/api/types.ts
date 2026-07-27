export interface MeResponse {
  id: string
  email: string
  displayName: string
}

export interface PropertyDto {
  id: string
  nickname: string
  address: string
  purchaseDate: string
  purchasePrice: number
  createdAt: string
}

export type DocumentCategory = 'Deed' | 'Warranty' | 'Receipt' | 'Photo' | 'Other'

export interface RenovationTypeDto {
  id: string
  name: string
  recommendedIntervalMonths: number | null
}

export interface ValuationEntryDto {
  id: string
  propertyId: string
  date: string
  value: number
  source: string | null
  notes: string | null
  createdByUserId: string
  createdAt: string
}

export interface RenovationEntryDto {
  id: string
  propertyId: string
  date: string
  renovationTypeId: string
  title: string
  description: string | null
  amount: number
  vendor: string | null
  createdByUserId: string
  createdAt: string
}

export interface DocumentDto {
  id: string
  propertyId: string
  renovationEntryId: string | null
  date: string
  fileName: string
  contentType: string
  sizeBytes: number
  category: DocumentCategory
  uploadedByUserId: string
  uploadedAt: string
}
