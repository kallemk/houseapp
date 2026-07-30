import type { CreatePropertyInput } from '../api/properties'
import type { PropertyDto, PropertyType } from '../api/types'

/**
 * The one property form, shared by "add a property" and "edit a property" on both the picker page
 * and the dashboard. Numbers are `number | string` because an empty optional field is '' rather
 * than 0 — see toInput, which turns those back into nulls.
 */
export interface PropertyFormValues {
  nickname: string
  address: string
  address2: string
  postalCode: string
  city: string
  country: string
  propertyDesignation: string
  yearBuilt: number | string
  type: PropertyType | ''
  latitude: number | string
  longitude: number | string
  purchaseDate: string
  purchasePrice: number | string
}

export const EMPTY_PROPERTY_FORM: PropertyFormValues = {
  nickname: '',
  address: '',
  address2: '',
  postalCode: '',
  city: '',
  country: 'Sverige',
  propertyDesignation: '',
  yearBuilt: '',
  type: '',
  latitude: '',
  longitude: '',
  purchaseDate: '',
  purchasePrice: '',
}

export function propertyToFormValues(property: PropertyDto): PropertyFormValues {
  return {
    nickname: property.nickname,
    address: property.address,
    address2: property.address2 ?? '',
    postalCode: property.postalCode ?? '',
    city: property.city ?? '',
    country: property.country ?? '',
    propertyDesignation: property.propertyDesignation ?? '',
    yearBuilt: property.yearBuilt ?? '',
    type: property.type ?? '',
    latitude: property.latitude ?? '',
    longitude: property.longitude ?? '',
    purchaseDate: property.purchaseDate,
    purchasePrice: property.purchasePrice,
  }
}

export function propertyFormToInput(values: PropertyFormValues): CreatePropertyInput {
  return {
    nickname: values.nickname,
    address: values.address,
    address2: values.address2.trim() || null,
    postalCode: values.postalCode.trim() || null,
    city: values.city.trim() || null,
    country: values.country.trim() || null,
    propertyDesignation: values.propertyDesignation.trim() || null,
    yearBuilt: values.yearBuilt === '' ? null : Number(values.yearBuilt),
    type: values.type === '' ? null : values.type,
    latitude: values.latitude === '' ? null : Number(values.latitude),
    longitude: values.longitude === '' ? null : Number(values.longitude),
    purchaseDate: values.purchaseDate,
    purchasePrice: Number(values.purchasePrice),
  }
}
