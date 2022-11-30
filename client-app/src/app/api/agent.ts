import axios, { AxiosError, AxiosRequestConfig, AxiosResponse } from 'axios'
import { Activity, ActivityFormValues } from '../models/activity'
import { toast } from 'react-toastify'
import { history } from './../../index'
import { User, UserFormValues } from '../models/user'
import { store } from './../stores/store'
import { request } from 'http'

const sleep = (delay: number) => {
  return new Promise((resolve) => {
    setTimeout(resolve, delay)
  })
}

axios.defaults.baseURL = 'http://localhost:5000/api'

axios.interceptors.request.use((config: AxiosRequestConfig) => {
  const token = store.commonStore.token
  config.headers = config.headers ?? {}

  config.headers.Authorization = `Bearer ${token}`

  return config
})

axios.interceptors.response.use(
  async (response) => {
    await sleep(1000)
    return response
  },
  (error: AxiosError) => {
    const { status } = error.response!
    switch (status) {
      case 400:
        toast.error('bad request')
        break
      case 401:
        toast.error('unauthorized')
        break
      case 404:
        history.push('/not-found')
        break
      case 500:
        toast.error('server error')
        break
    }

    return Promise.reject(error)
  }
)

const responseBody = <T>(response: AxiosResponse<T>) => response.data

const requests = {
  get: <T>(url: string) => axios.get<T>(url).then(responseBody),
  post: <T>(url: string, body: {}) =>
    axios.post<T>(url, body).then(responseBody),
  put: <T>(url: string, body: {}) => axios.put<T>(url, body).then(responseBody),
  delete: <T>(url: string) => axios.delete<T>(url).then(responseBody),
}

const Activities = {
  list: () => requests.get<Activity[]>('/activities'),
  details: (id: string) => requests.get<Activity>(`/activities/${id}`),
  create: (activity: ActivityFormValues) =>
    requests.post<void>('/activities', activity),
  update: (activity: ActivityFormValues) =>
    requests.put<void>(`/activities/${activity.id}`, activity),
  delete: (id: string) => requests.delete(`/activities/${id}`),
  attend: (id: string) => requests.post<void>(`/activities/${id}/attend`, {}),
}

const Account = {
  current: () => requests.get<User>('/account'),
  login: (user: UserFormValues) => requests.post<User>('account/login', user),
  register: (user: UserFormValues) =>
    requests.post<User>('account/register', user),
}

const agent = {
  Activities,
  Account,
}

export default agent
